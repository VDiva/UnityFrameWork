using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace FrameWork.Script.WebNet.CodeGen
{
    /// <summary>
    /// 把带 WebClientRpcAttribute 的业务方法改写为发送包装方法，并把原方法体保存到生成的方法中。
    /// </summary>
    internal sealed class WebRpcILPostProcessor : ILPostProcessor
    {
        const string RpcAttributeFullName = "FrameWork.Script.WebNet.WebClientRpcAttribute";
        const string NetworkBehaviourFullName = "FrameWork.Script.WebNet.WebNetworkBehaviour";
        const string DispatcherFullName = "FrameWork.Script.WebNet.WebNetworkRpcDispatcher";
        const string GeneratedPrefix = "__WebRpcUserCode_";

        public override ILPostProcessor GetInstance()
        {
            return new WebRpcILPostProcessor();
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly == null ||
                compiledAssembly.InMemoryAssembly.PeData == null ||
                compiledAssembly.InMemoryAssembly.PdbData == null)
            {
                return false;
            }

            // Web 网络运行时代码目前位于 Assembly-CSharp；避免扫描 Unity 和第三方程序集。
            return string.Equals(compiledAssembly.Name, "Assembly-CSharp", StringComparison.Ordinal);
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;

            var diagnostics = new List<DiagnosticMessage>();
            try
            {
                using var peInput = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData);
                using var pdbInput = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData);
                // ILPostProcessor 读取的是内存中的 Assembly-CSharp，Cecil 默认不知道
                // UnityEngine.CoreModule 等 Unity 程序集位于哪里。把编译管线提供的所有
                // 引用目录注册到 resolver，解析基类、枚举和自定义特性时才能找到它们。
                using var assemblyResolver = new DefaultAssemblyResolver();
                foreach (string reference in compiledAssembly.References ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(reference))
                        continue;

                    string directory = Path.GetDirectoryName(reference);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        assemblyResolver.AddSearchDirectory(directory);
                }

                var readerParameters = new ReaderParameters
                {
                    ReadingMode = ReadingMode.Immediate,
                    ReadSymbols = true,
                    SymbolStream = pdbInput,
                    SymbolReaderProvider = new PortablePdbReaderProvider(),
                    AssemblyResolver = assemblyResolver
                };
                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(peInput, readerParameters);
                ModuleDefinition module = assembly.MainModule;

                TypeDefinition dispatcher = FindType(module, DispatcherFullName);
                MethodDefinition tryRelay = dispatcher?.Methods.FirstOrDefault(method =>
                    method.Name == "TryRelay" &&
                    method.IsStatic &&
                    method.Parameters.Count == 3);
                if (tryRelay == null)
                {
                    diagnostics.Add(Error("WEBRPC001",
                        $"找不到 {DispatcherFullName}.TryRelay，无法生成 WebClientRpc。"));
                    return new ILPostProcessResult(null, diagnostics);
                }

                var rewrites = new List<RpcRewrite>();
                foreach (TypeDefinition type in GetAllTypes(module))
                {
                    if (!InheritsFrom(type, NetworkBehaviourFullName))
                        continue;

                    // 复制列表，因为改写时会向 type.Methods 添加包装方法。
                    foreach (MethodDefinition method in type.Methods.ToArray())
                    {
                        CustomAttribute rpcAttribute = method.CustomAttributes.FirstOrDefault(attribute =>
                            attribute.AttributeType.FullName == RpcAttributeFullName);
                        if (rpcAttribute == null || method.Name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                            continue;

                        if (!ValidateRpc(method, diagnostics))
                            continue;

                        MethodDefinition wrapper = CreateWrapper(module, method, rpcAttribute, tryRelay);
                        rewrites.Add(new RpcRewrite(method, wrapper));
                    }
                }

                if (diagnostics.Any(message => message.DiagnosticType == DiagnosticType.Error))
                    return new ILPostProcessResult(null, diagnostics);
                if (rewrites.Count == 0)
                    return null;

                // 原程序集内部的调用仍指向被改名的方法定义，需要统一改为调用新包装方法。
                foreach (TypeDefinition type in GetAllTypes(module))
                foreach (MethodDefinition caller in type.Methods)
                {
                    if (!caller.HasBody)
                        continue;

                    foreach (Instruction instruction in caller.Body.Instructions)
                    {
                        if (!(instruction.Operand is MethodReference calledMethod))
                            continue;

                        foreach (RpcRewrite rewrite in rewrites)
                        {
                            // 用户方法体中的递归调用，以及包装器末尾对用户方法体的调用，
                            // 都必须保留原目标。其余调用点才改为调用 RPC 包装器。
                            if (caller == rewrite.UserCode || caller == rewrite.Wrapper)
                                continue;
                            if (ReferencesMethod(calledMethod, rewrite.UserCode))
                            {
                                instruction.Operand = rewrite.Wrapper;
                                break;
                            }
                        }
                    }
                }

                using var peOutput = new MemoryStream();
                using var pdbOutput = new MemoryStream();
                assembly.Write(peOutput, new WriterParameters
                {
                    WriteSymbols = true,
                    SymbolStream = pdbOutput,
                    SymbolWriterProvider = new PortablePdbWriterProvider()
                });
                return new ILPostProcessResult(
                    new InMemoryAssembly(peOutput.ToArray(), pdbOutput.ToArray()),
                    diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Error("WEBRPC999",
                    $"WebRpc Weaver 内部错误：{exception}"));
                return new ILPostProcessResult(null, diagnostics);
            }
        }

        static MethodDefinition CreateWrapper(
            ModuleDefinition module,
            MethodDefinition userCode,
            CustomAttribute rpcAttribute,
            MethodDefinition tryRelay)
        {
            string originalName = userCode.Name;
            string generatedName =
                $"{GeneratedPrefix}{originalName}_{userCode.MetadataToken.RID:X8}";

            MethodAttributes originalAttributes = userCode.Attributes;
            var wrapper = new MethodDefinition(
                originalName,
                originalAttributes,
                module.ImportReference(userCode.ReturnType))
            {
                ImplAttributes = userCode.ImplAttributes,
                IsPInvokeImpl = false
            };

            foreach (ParameterDefinition parameter in userCode.Parameters)
            {
                var wrapperParameter = new ParameterDefinition(
                    parameter.Name,
                    parameter.Attributes,
                    module.ImportReference(parameter.ParameterType));
                if (parameter.HasConstant)
                {
                    wrapperParameter.HasConstant = true;
                    wrapperParameter.Constant = parameter.Constant;
                }
                wrapper.Parameters.Add(wrapperParameter);
            }

            // 方法特性都留在公开包装方法上，远端反射和 Unity 特性仍作用于原方法名。
            foreach (CustomAttribute attribute in userCode.CustomAttributes.ToArray())
            {
                userCode.CustomAttributes.Remove(attribute);
                wrapper.CustomAttributes.Add(attribute);
            }
            for (int index = 0; index < userCode.Parameters.Count; index++)
            {
                ParameterDefinition source = userCode.Parameters[index];
                ParameterDefinition destination = wrapper.Parameters[index];
                foreach (CustomAttribute attribute in source.CustomAttributes.ToArray())
                {
                    source.CustomAttributes.Remove(attribute);
                    destination.CustomAttributes.Add(attribute);
                }
            }

            userCode.Name = generatedName;
            userCode.Attributes &= ~(MethodAttributes.MemberAccessMask |
                                     MethodAttributes.Virtual |
                                     MethodAttributes.NewSlot |
                                     MethodAttributes.Abstract);
            userCode.Attributes |= MethodAttributes.Private | MethodAttributes.HideBySig;
            userCode.ImplAttributes &= ~(MethodImplAttributes.InternalCall |
                                         MethodImplAttributes.Native |
                                         MethodImplAttributes.Runtime);
            userCode.ImplAttributes |= MethodImplAttributes.IL;

            TypeDefinition declaringType = userCode.DeclaringType;
            declaringType.Methods.Add(wrapper);

            ILProcessor il = wrapper.Body.GetILProcessor();
            Instruction invokeUserCode = il.Create(OpCodes.Nop);

            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldstr, originalName));
            EmitLoadArgumentsArray(module, wrapper, il);
            il.Append(il.Create(OpCodes.Call, module.ImportReference(tryRelay)));
            il.Append(il.Create(OpCodes.Brfalse, invokeUserCode));
            il.Append(il.Create(OpCodes.Ret));
            il.Append(invokeUserCode);
            il.Append(il.Create(OpCodes.Ldarg_0));
            for (int index = 0; index < wrapper.Parameters.Count; index++)
                il.Append(il.Create(OpCodes.Ldarg, wrapper.Parameters[index]));
            il.Append(il.Create(OpCodes.Call, userCode));
            il.Append(il.Create(OpCodes.Ret));

            return wrapper;
        }

        static void EmitLoadArgumentsArray(
            ModuleDefinition module,
            MethodDefinition wrapper,
            ILProcessor il)
        {
            il.Append(il.Create(OpCodes.Ldc_I4, wrapper.Parameters.Count));
            il.Append(il.Create(OpCodes.Newarr, module.TypeSystem.Object));
            for (int index = 0; index < wrapper.Parameters.Count; index++)
            {
                ParameterDefinition parameter = wrapper.Parameters[index];
                il.Append(il.Create(OpCodes.Dup));
                il.Append(il.Create(OpCodes.Ldc_I4, index));
                il.Append(il.Create(OpCodes.Ldarg, parameter));
                if (parameter.ParameterType.IsValueType)
                    il.Append(il.Create(OpCodes.Box, module.ImportReference(parameter.ParameterType)));
                il.Append(il.Create(OpCodes.Stelem_Ref));
            }
        }

        static bool ValidateRpc(
            MethodDefinition method,
            ICollection<DiagnosticMessage> diagnostics)
        {
            bool valid = true;
            if (method.IsStatic || method.IsAbstract || !method.HasBody)
            {
                diagnostics.Add(Error("WEBRPC100",
                    $"{method.FullName} 必须是有方法体的实例方法。", method));
                valid = false;
            }
            if (method.ReturnType.FullName != "System.Void")
            {
                diagnostics.Add(Error("WEBRPC101",
                    $"{method.FullName} 必须返回 void。", method));
                valid = false;
            }
            if (method.HasGenericParameters || method.DeclaringType.HasGenericParameters)
            {
                diagnostics.Add(Error("WEBRPC102",
                    $"{method.FullName} 暂不支持泛型 RPC。", method));
                valid = false;
            }
            if (method.IsVirtual)
            {
                diagnostics.Add(Error("WEBRPC103",
                    $"{method.FullName} 暂不支持 virtual/override RPC。", method));
                valid = false;
            }
            foreach (ParameterDefinition parameter in method.Parameters)
            {
                if (parameter.ParameterType.IsByReference || parameter.IsOut)
                {
                    diagnostics.Add(Error("WEBRPC104",
                        $"{method.FullName} 不支持 ref/out 参数。", method));
                    valid = false;
                }
                else if (!IsSupportedRpcType(parameter.ParameterType))
                {
                    diagnostics.Add(Error("WEBRPC105",
                        $"{method.FullName} 的参数 {parameter.Name} 类型不受支持：{parameter.ParameterType.FullName}。",
                        method));
                    valid = false;
                }
            }
            return valid;
        }

        static bool IsSupportedRpcType(TypeReference type)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                case "System.Single":
                case "System.Double":
                case "System.String":
                case "System.Byte[]":
                case "UnityEngine.Vector2":
                case "UnityEngine.Vector3":
                case "UnityEngine.Quaternion":
                    return true;
            }

            try
            {
                return type.Resolve()?.IsEnum == true;
            }
            catch
            {
                return false;
            }
        }

        static bool ReferencesMethod(MethodReference reference, MethodDefinition target)
        {
            if (ReferenceEquals(reference, target))
                return true;
            try
            {
                if (reference.Resolve() == target)
                    return true;
            }
            catch
            {
                // 无法解析外部引用时继续使用同模块 token 判断。
            }
            return reference.MetadataToken == target.MetadataToken &&
                   reference.Module == target.Module;
        }

        static bool InheritsFrom(TypeDefinition type, string baseTypeFullName)
        {
            for (TypeReference current = type; current != null;)
            {
                if (current.FullName == baseTypeFullName)
                    return true;
                try
                {
                    current = current.Resolve()?.BaseType;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        static TypeDefinition FindType(ModuleDefinition module, string fullName)
        {
            return GetAllTypes(module).FirstOrDefault(type => type.FullName == fullName);
        }

        static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
        {
            foreach (TypeDefinition type in module.Types)
            foreach (TypeDefinition nested in GetTypeAndNestedTypes(type))
                yield return nested;
        }

        static IEnumerable<TypeDefinition> GetTypeAndNestedTypes(TypeDefinition type)
        {
            yield return type;
            foreach (TypeDefinition nested in type.NestedTypes)
            foreach (TypeDefinition descendant in GetTypeAndNestedTypes(nested))
                yield return descendant;
        }

        static DiagnosticMessage Error(
            string code,
            string text,
            MethodDefinition method = null)
        {
            var diagnostic = new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Error,
                MessageData = $"error {code}: {text}",
                File = string.Empty,
                Line = 0,
                Column = 0
            };

            SequencePoint sequencePoint = method?.DebugInformation?.SequencePoints?.FirstOrDefault();
            if (sequencePoint != null)
            {
                diagnostic.File = sequencePoint.Document?.Url ?? string.Empty;
                diagnostic.Line = sequencePoint.StartLine;
                diagnostic.Column = sequencePoint.StartColumn;
            }
            return diagnostic;
        }

        readonly struct RpcRewrite
        {
            public RpcRewrite(MethodDefinition userCode, MethodDefinition wrapper)
            {
                UserCode = userCode;
                Wrapper = wrapper;
            }

            public MethodDefinition UserCode { get; }
            public MethodDefinition Wrapper { get; }
        }
    }
}
