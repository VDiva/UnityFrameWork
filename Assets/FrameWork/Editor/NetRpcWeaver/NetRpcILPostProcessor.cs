#if CECIL_INSTALLED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace FrameWork.Net.CodeGen
{
    /// <summary>
    /// 把带 ClientRpcAttribute 的业务方法改写为发送包装方法，并把原方法体保存到生成的方法中。
    /// </summary>
    internal sealed class NetRpcILPostProcessor : ILPostProcessor
    {
        const string RpcAttributeFullName = "FrameWork.Net.Network.ClientRpcAttribute";
        const string SyncVarAttributeFullName = "FrameWork.Net.Network.SyncVarAttribute";
        const string MonoBehaviourFullName = "UnityEngine.MonoBehaviour";
        const string NetworkBehaviourFullName = "FrameWork.Net.Network.NetworkBehaviour";
        const string NetworkIdentityFullName = "FrameWork.Net.Network.NetworkIdentity";
        const string AsyncStateMachineAttributeFullName = "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
        const string DispatcherFullName = "FrameWork.Net.Network.NetworkRpcDispatcher";
        const string GeneratedPrefix = "__NetRpcUserCode_";
        const string FastInvokePrefix = "__NetRpcInvoke_";
        const string SyncGetPrefix = "__NetSyncGet_";
        const string SyncSetPrefix = "__NetSyncSet_";

        public override ILPostProcessor GetInstance()
        {
            return new NetRpcILPostProcessor();
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly == null ||
                compiledAssembly.InMemoryAssembly.PeData == null)
            {
                return false;
            }

            // Web 网络运行时代码目前位于 HotUpdate；避免扫描 Unity 和第三方程序集。
            return compiledAssembly.Name == "HotUpdate" ||
                   compiledAssembly.Name.StartsWith("HotUpdate.", StringComparison.Ordinal);
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;

            var diagnostics = new List<DiagnosticMessage>();
            try
            {
                bool hasSymbols = compiledAssembly.InMemoryAssembly.PdbData != null &&
                                  compiledAssembly.InMemoryAssembly.PdbData.Length > 0;
                using var peInput = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData);
                using var pdbInput = hasSymbols ? new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData) : null;
                // ILPostProcessor 读取的是内存中的程序集，把编译管线提供的所有
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
                    ReadSymbols = hasSymbols,
                    SymbolStream = pdbInput,
                    SymbolReaderProvider = hasSymbols ? new PortablePdbReaderProvider() : null,
                    AssemblyResolver = assemblyResolver
                };
                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(peInput, readerParameters);
                ModuleDefinition module = assembly.MainModule;

                TypeDefinition dispatcher = FindType(module, DispatcherFullName);
                if (dispatcher == null)
                {
                    var runtimeReference = module.AssemblyReferences.FirstOrDefault(r => r.Name == "HotUpdate");
                    if (runtimeReference != null)
                        dispatcher = FindType(assemblyResolver.Resolve(runtimeReference).MainModule, DispatcherFullName);
                }

                MethodDefinition beginFastRelay = dispatcher?.Methods.FirstOrDefault(method =>
                    method.Name == "BeginFastRelay" && method.IsStatic && method.Parameters.Count == 6);
                MethodDefinition endFastRelay = dispatcher?.Methods.FirstOrDefault(method =>
                    method.Name == "EndFastRelay" && method.IsStatic && method.Parameters.Count == 4);
                MethodDefinition tryRelayBehaviour = dispatcher?.Methods.FirstOrDefault(method =>
                    method.Name == "TryRelay" &&
                    method.IsStatic &&
                    method.Parameters.Count == 3 &&
                    method.Parameters[0].ParameterType.FullName == NetworkBehaviourFullName);
                MethodDefinition tryRelayIdentity = dispatcher?.Methods.FirstOrDefault(method =>
                    method.Name == "TryRelay" &&
                    method.IsStatic &&
                    method.Parameters.Count == 3 &&
                    method.Parameters[0].ParameterType.FullName == NetworkIdentityFullName);
                if (tryRelayBehaviour == null || beginFastRelay == null || endFastRelay == null)
                {
                    diagnostics.Add(Error("NETRPC001",
                        $"找不到 {DispatcherFullName} 所需的 RPC 分发入口，无法生成 ClientRpc。"));
                    return new ILPostProcessResult(null, diagnostics);
                }

                var rewrites = new List<RpcRewrite>();
                int syncAccessorsAdded = 0;
                foreach (TypeDefinition type in GetAllTypes(module))
                {
                    bool isBehaviour = InheritsFrom(type, NetworkBehaviourFullName);
                    bool isIdentity = !isBehaviour && type.FullName == NetworkIdentityFullName;
                    bool isMono = isBehaviour || isIdentity || InheritsFrom(type, MonoBehaviourFullName);

                    if (isMono && !type.HasGenericParameters)
                    {
                        foreach (FieldDefinition field in type.Fields)
                        {
                            if (field.IsStatic)
                                continue;
                            bool hasSyncVar = field.CustomAttributes.Any(a =>
                                a.AttributeType.FullName == SyncVarAttributeFullName);
                            if (hasSyncVar && TryEmitSyncVarAccessors(module, type, field))
                                syncAccessorsAdded++;
                        }
                    }

                    // 复制列表，因为改写时会向 type.Methods 添加包装方法。
                    foreach (MethodDefinition method in type.Methods.ToArray())
                    {
                        CustomAttribute rpcAttribute = method.CustomAttributes.FirstOrDefault(attribute =>
                            attribute.AttributeType.FullName == RpcAttributeFullName);
                        if (rpcAttribute == null || method.Name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                            continue;

                        if (!isBehaviour && !isIdentity)
                        {
                            diagnostics.Add(Error("NETRPC106",
                                $"{method.FullName} 标记了 [ClientRpc]，但所在类型 {type.FullName} 未继承 {NetworkBehaviourFullName}。", method));
                            continue;
                        }

                        if (!ValidateRpc(method, diagnostics))
                            continue;

                        MethodDefinition wrapper = CreateWrapper(
                            module, dispatcher, method, rpcAttribute, beginFastRelay, endFastRelay);
                        EmitFastInvokeStub(module, dispatcher, type, method, wrapper);
                        rewrites.Add(new RpcRewrite(method, wrapper));
                    }
                }

                if (diagnostics.Any(message => message.DiagnosticType == DiagnosticType.Error))
                    return new ILPostProcessResult(null, diagnostics);
                if (rewrites.Count == 0 && syncAccessorsAdded == 0)
                    return null;

                if (rewrites.Count > 0)
                {
                    var rewriteByToken = new Dictionary<MetadataToken, RpcRewrite>(rewrites.Count);
                    foreach (RpcRewrite rewrite in rewrites)
                        rewriteByToken[rewrite.UserCode.MetadataToken] = rewrite;

                    // 原程序集内部的调用仍指向被改名的方法定义，使用 O(1) 索引统一改为调用新包装方法。
                    foreach (TypeDefinition type in GetAllTypes(module))
                    foreach (MethodDefinition caller in type.Methods)
                    {
                        if (!caller.HasBody)
                            continue;

                        foreach (Instruction instruction in caller.Body.Instructions)
                        {
                            if (!(instruction.Operand is MethodReference calledMethod) || calledMethod.Module != module)
                                continue;

                            if (rewriteByToken.TryGetValue(calledMethod.MetadataToken, out RpcRewrite rewrite))
                            {
                                if (caller == rewrite.UserCode || caller == rewrite.Wrapper)
                                    continue;
                                instruction.Operand = rewrite.Wrapper;
                            }
                        }
                    }
                }

                using var peOutput = new MemoryStream();
                using var pdbOutput = hasSymbols ? new MemoryStream() : null;
                assembly.Write(peOutput, new WriterParameters
                {
                    WriteSymbols = hasSymbols,
                    SymbolStream = pdbOutput,
                    SymbolWriterProvider = hasSymbols ? new PortablePdbWriterProvider() : null
                });
                return new ILPostProcessResult(
                    new InMemoryAssembly(peOutput.ToArray(), hasSymbols ? pdbOutput.ToArray() : null),
                    diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Error("NETRPC999",
                    $"NetRpc Weaver 内部错误：{exception}"));
                return new ILPostProcessResult(null, diagnostics);
            }
        }

        static bool TryEmitSyncVarAccessors(ModuleDefinition module, TypeDefinition type, FieldDefinition field)
        {
            if (!IsSupportedRpcType(field.FieldType))
                return false;

            string getterName = SyncGetPrefix + field.Name;
            string setterName = SyncSetPrefix + field.Name;
            if (type.Methods.Any(m => m.Name == getterName || m.Name == setterName))
                return false;

            TypeDefinition resolvedEnum = null;
            try
            {
                TypeDefinition resolved = field.FieldType.Resolve();
                if (resolved != null && resolved.IsEnum)
                    resolvedEnum = resolved;
            }
            catch { }

            const MethodAttributes accessorAttrs =
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

            if (resolvedEnum != null)
            {
                FieldDefinition valueField = resolvedEnum.Fields.FirstOrDefault(f => !f.IsStatic);
                if (valueField == null)
                    return false;
                string underlying = valueField.FieldType.FullName;

                var getter = new MethodDefinition(getterName, accessorAttrs, module.TypeSystem.Int64);
                ILProcessor getIl = getter.Body.GetILProcessor();
                getIl.Append(getIl.Create(OpCodes.Ldarg_0));
                getIl.Append(getIl.Create(OpCodes.Ldfld, field));
                if (underlying == "System.Byte" || underlying == "System.UInt16" || underlying == "System.UInt32")
                    getIl.Append(getIl.Create(OpCodes.Conv_U8));
                else if (underlying != "System.Int64" && underlying != "System.UInt64")
                    getIl.Append(getIl.Create(OpCodes.Conv_I8));
                getIl.Append(getIl.Create(OpCodes.Ret));
                type.Methods.Add(getter);

                var setter = new MethodDefinition(setterName, accessorAttrs, module.TypeSystem.Void);
                setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.Int64));
                ILProcessor setIl = setter.Body.GetILProcessor();
                setIl.Append(setIl.Create(OpCodes.Ldarg_0));
                setIl.Append(setIl.Create(OpCodes.Ldarg_1));
                switch (underlying)
                {
                    case "System.SByte": setIl.Append(setIl.Create(OpCodes.Conv_I1)); break;
                    case "System.Byte": setIl.Append(setIl.Create(OpCodes.Conv_U1)); break;
                    case "System.Int16": setIl.Append(setIl.Create(OpCodes.Conv_I2)); break;
                    case "System.UInt16": setIl.Append(setIl.Create(OpCodes.Conv_U2)); break;
                    case "System.Int32": setIl.Append(setIl.Create(OpCodes.Conv_I4)); break;
                    case "System.UInt32": setIl.Append(setIl.Create(OpCodes.Conv_U4)); break;
                }
                setIl.Append(setIl.Create(OpCodes.Stfld, field));
                setIl.Append(setIl.Create(OpCodes.Ret));
                type.Methods.Add(setter);
                return true;
            }

            TypeReference fieldTypeRef = module.ImportReference(field.FieldType);
            var typedGetter = new MethodDefinition(getterName, accessorAttrs, fieldTypeRef);
            ILProcessor tgIl = typedGetter.Body.GetILProcessor();
            tgIl.Append(tgIl.Create(OpCodes.Ldarg_0));
            tgIl.Append(tgIl.Create(OpCodes.Ldfld, field));
            tgIl.Append(tgIl.Create(OpCodes.Ret));
            type.Methods.Add(typedGetter);

            var typedSetter = new MethodDefinition(setterName, accessorAttrs, module.TypeSystem.Void);
            typedSetter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, fieldTypeRef));
            ILProcessor tsIl = typedSetter.Body.GetILProcessor();
            tsIl.Append(tsIl.Create(OpCodes.Ldarg_0));
            tsIl.Append(tsIl.Create(OpCodes.Ldarg_1));
            tsIl.Append(tsIl.Create(OpCodes.Stfld, field));
            tsIl.Append(tsIl.Create(OpCodes.Ret));
            type.Methods.Add(typedSetter);
            return true;
        }

        static MethodDefinition CreateWrapper(
            ModuleDefinition module,
            TypeDefinition dispatcher,
            MethodDefinition userCode,
            CustomAttribute rpcAttribute,
            MethodDefinition beginFastRelay,
            MethodDefinition endFastRelay)
        {
            string originalName = userCode.Name;
            uint methodId = ComputeMethodId(userCode);
            bool includeSender = GetIncludeSender(rpcAttribute);
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

            wrapper.Body.InitLocals = true;
            var writerLocal = new VariableDefinition(module.ImportReference(beginFastRelay.ReturnType));
            var behaviourIdLocal = new VariableDefinition(module.TypeSystem.UInt32);
            var execLocal = new VariableDefinition(module.TypeSystem.Boolean);
            wrapper.Body.Variables.Add(writerLocal);
            wrapper.Body.Variables.Add(behaviourIdLocal);
            wrapper.Body.Variables.Add(execLocal);

            ILProcessor il = wrapper.Body.GetILProcessor();
            Instruction checkLocalExec = il.Create(OpCodes.Nop);
            Instruction retInstruction = il.Create(OpCodes.Ret);

            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldc_I4, unchecked((int)methodId)));
            il.Append(il.Create(OpCodes.Ldc_I4, wrapper.Parameters.Count));
            il.Append(il.Create(includeSender ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            il.Append(il.Create(OpCodes.Ldloca, behaviourIdLocal));
            il.Append(il.Create(OpCodes.Ldloca, execLocal));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(beginFastRelay)));
            il.Append(il.Create(OpCodes.Stloc, writerLocal));

            il.Append(il.Create(OpCodes.Ldloc, writerLocal));
            il.Append(il.Create(OpCodes.Brfalse, checkLocalExec));

            for (int index = 0; index < wrapper.Parameters.Count; index++)
            {
                EmitWriteParameter(module, dispatcher, il, writerLocal, wrapper.Parameters[index]);
            }

            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldloc, behaviourIdLocal));
            il.Append(il.Create(OpCodes.Ldc_I4, unchecked((int)methodId)));
            il.Append(il.Create(includeSender ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(endFastRelay)));

            il.Append(checkLocalExec);
            il.Append(il.Create(OpCodes.Ldloc, execLocal));
            il.Append(il.Create(OpCodes.Brfalse, retInstruction));

            il.Append(il.Create(OpCodes.Ldarg_0));
            for (int index = 0; index < wrapper.Parameters.Count; index++)
                il.Append(il.Create(OpCodes.Ldarg, wrapper.Parameters[index]));
            il.Append(il.Create(OpCodes.Call, userCode));
            il.Append(retInstruction);

            return wrapper;
        }

        static void EmitFastInvokeStub(
            ModuleDefinition module,
            TypeDefinition dispatcher,
            TypeDefinition declaringType,
            MethodDefinition userCode,
            MethodDefinition wrapper)
        {
            uint methodId = ComputeMethodIdFromSignature(declaringType, wrapper.Name, wrapper.Parameters);
            string stubName = $"{FastInvokePrefix}{methodId:X8}";
            if (declaringType.Methods.Any(m => m.Name == stubName))
                return;

            MethodDefinition readInt32 = dispatcher.Methods.First(m => m.Name == "ReadInt32" && m.IsStatic);
            TypeReference monoBehaviourRef = module.ImportReference(dispatcher.Methods.First(m => m.Name == "BeginFastRelay").Parameters[0].ParameterType);
            TypeReference binaryReaderRef = module.ImportReference(readInt32.Parameters[0].ParameterType);

            var stub = new MethodDefinition(
                stubName,
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                module.TypeSystem.Boolean);
            stub.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, monoBehaviourRef));
            stub.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, binaryReaderRef));
            stub.Body.InitLocals = true;

            var paramLocals = new VariableDefinition[wrapper.Parameters.Count];
            for (int i = 0; i < wrapper.Parameters.Count; i++)
            {
                paramLocals[i] = new VariableDefinition(module.ImportReference(wrapper.Parameters[i].ParameterType));
                stub.Body.Variables.Add(paramLocals[i]);
            }

            ILProcessor il = stub.Body.GetILProcessor();
            Instruction failLabel = il.Create(OpCodes.Ldc_I4_0);

            // if (ReadInt32(reader) != paramCount) return false;
            il.Append(il.Create(OpCodes.Ldarg_1));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(readInt32)));
            il.Append(il.Create(OpCodes.Ldc_I4, wrapper.Parameters.Count));
            il.Append(il.Create(OpCodes.Bne_Un, failLabel));

            for (int i = 0; i < wrapper.Parameters.Count; i++)
            {
                EmitReadParameter(module, dispatcher, il, wrapper.Parameters[i].ParameterType, paramLocals[i]);
            }

            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Castclass, declaringType));
            for (int i = 0; i < paramLocals.Length; i++)
                il.Append(il.Create(OpCodes.Ldloc, paramLocals[i]));
            il.Append(il.Create(OpCodes.Call, userCode));
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Ret));

            il.Append(failLabel);
            il.Append(il.Create(OpCodes.Ret));
            declaringType.Methods.Add(stub);
        }

        static void EmitWriteParameter(
            ModuleDefinition module,
            TypeDefinition dispatcher,
            ILProcessor il,
            VariableDefinition writerLocal,
            ParameterDefinition parameter)
        {
            il.Append(il.Create(OpCodes.Ldloc, writerLocal));
            il.Append(il.Create(OpCodes.Ldarg, parameter));

            string writeHelper = GetWriteHelperName(parameter.ParameterType, out string enumUnderlying);
            if (enumUnderlying != null)
            {
                if (enumUnderlying == "System.Byte" || enumUnderlying == "System.UInt16" || enumUnderlying == "System.UInt32")
                    il.Append(il.Create(OpCodes.Conv_U8));
                else if (enumUnderlying != "System.Int64" && enumUnderlying != "System.UInt64")
                    il.Append(il.Create(OpCodes.Conv_I8));
            }

            MethodDefinition helper = dispatcher.Methods.First(m => m.Name == writeHelper && m.IsStatic);
            il.Append(il.Create(OpCodes.Call, module.ImportReference(helper)));
        }

        static void EmitReadParameter(
            ModuleDefinition module,
            TypeDefinition dispatcher,
            ILProcessor il,
            TypeReference paramType,
            VariableDefinition local)
        {
            string readHelper = GetReadHelperName(paramType, out string enumUnderlying);
            MethodDefinition helper = dispatcher.Methods.First(m => m.Name == readHelper && m.IsStatic);
            il.Append(il.Create(OpCodes.Ldarg_1));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(helper)));
            if (enumUnderlying != null)
            {
                switch (enumUnderlying)
                {
                    case "System.SByte": il.Append(il.Create(OpCodes.Conv_I1)); break;
                    case "System.Byte": il.Append(il.Create(OpCodes.Conv_U1)); break;
                    case "System.Int16": il.Append(il.Create(OpCodes.Conv_I2)); break;
                    case "System.UInt16": il.Append(il.Create(OpCodes.Conv_U2)); break;
                    case "System.Int32": il.Append(il.Create(OpCodes.Conv_I4)); break;
                    case "System.UInt32": il.Append(il.Create(OpCodes.Conv_U4)); break;
                }
            }
            il.Append(il.Create(OpCodes.Stloc, local));
        }

        static string GetWriteHelperName(TypeReference type, out string enumUnderlying)
        {
            enumUnderlying = GetEnumUnderlyingFullName(type);
            if (enumUnderlying != null) return "WriteInt64";
            switch (type.FullName)
            {
                case "System.Boolean": return "WriteBool";
                case "System.Byte": return "WriteByte";
                case "System.SByte": return "WriteSByte";
                case "System.Int16": return "WriteInt16";
                case "System.UInt16": return "WriteUInt16";
                case "System.Int32": return "WriteInt32";
                case "System.UInt32": return "WriteUInt32";
                case "System.Int64": return "WriteInt64";
                case "System.UInt64": return "WriteUInt64";
                case "System.Single": return "WriteSingle";
                case "System.Double": return "WriteDouble";
                case "System.String": return "WriteString";
                case "System.Byte[]": return "WriteBytes";
                case "UnityEngine.Vector2": return "WriteVector2";
                case "UnityEngine.Vector3": return "WriteVector3";
                case "UnityEngine.Quaternion": return "WriteQuaternion";
                default: throw new InvalidOperationException($"Unsupported RPC type: {type.FullName}");
            }
        }

        static string GetReadHelperName(TypeReference type, out string enumUnderlying)
        {
            enumUnderlying = GetEnumUnderlyingFullName(type);
            if (enumUnderlying != null) return "ReadInt64";
            switch (type.FullName)
            {
                case "System.Boolean": return "ReadBool";
                case "System.Byte": return "ReadByte";
                case "System.SByte": return "ReadSByte";
                case "System.Int16": return "ReadInt16";
                case "System.UInt16": return "ReadUInt16";
                case "System.Int32": return "ReadInt32";
                case "System.UInt32": return "ReadUInt32";
                case "System.Int64": return "ReadInt64";
                case "System.UInt64": return "ReadUInt64";
                case "System.Single": return "ReadSingle";
                case "System.Double": return "ReadDouble";
                case "System.String": return "ReadString";
                case "System.Byte[]": return "ReadBytes";
                case "UnityEngine.Vector2": return "ReadVector2";
                case "UnityEngine.Vector3": return "ReadVector3";
                case "UnityEngine.Quaternion": return "ReadQuaternion";
                default: throw new InvalidOperationException($"Unsupported RPC type: {type.FullName}");
            }
        }

        static string GetEnumUnderlyingFullName(TypeReference type)
        {
            try
            {
                TypeDefinition resolved = type.Resolve();
                if (resolved != null && resolved.IsEnum)
                    return resolved.Fields.FirstOrDefault(f => !f.IsStatic)?.FieldType.FullName;
            }
            catch { }
            return null;
        }

        static bool GetIncludeSender(CustomAttribute rpcAttribute)
        {
            if (rpcAttribute == null || !rpcAttribute.HasProperties)
                return false;
            foreach (CustomAttributeNamedArgument prop in rpcAttribute.Properties)
            {
                if (prop.Name == "IncludeSender" && prop.Argument.Value is bool b)
                    return b;
            }
            return false;
        }

        static uint ComputeMethodId(MethodDefinition method) =>
            ComputeMethodIdFromSignature(method.DeclaringType, method.Name, method.Parameters);

        static uint ComputeMethodIdFromSignature(
            TypeReference declaringType,
            string methodName,
            IList<ParameterDefinition> parameters)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(ToReflectionFullName(declaringType)).Append('.').Append(methodName).Append('(');
            for (int i = 0; i < parameters.Count; i++)
                sb.Append(ToReflectionFullName(parameters[i].ParameterType)).Append(';');
            sb.Append(')');
            return StableHash(sb.ToString());
        }

        static string ToReflectionFullName(TypeReference type) =>
            (type?.FullName ?? string.Empty).Replace('/', '+');

        static uint StableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash == 0 ? 1u : hash;
        }

        static bool ValidateRpc(
            MethodDefinition method,
            ICollection<DiagnosticMessage> diagnostics)
        {
            bool valid = true;
            if (method.IsStatic || method.IsAbstract || !method.HasBody)
            {
                diagnostics.Add(Error("NETRPC100",
                    $"{method.FullName} 必须是有方法体的实例方法。", method));
                valid = false;
            }
            if (method.ReturnType.FullName != "System.Void")
            {
                diagnostics.Add(Error("NETRPC101",
                    $"{method.FullName} 必须返回 void。", method));
                valid = false;
            }
            if (method.CustomAttributes.Any(a => a.AttributeType.FullName == AsyncStateMachineAttributeFullName))
            {
                diagnostics.Add(Error("NETRPC107",
                    $"{method.FullName} 不支持 async void RPC，请在同步 RPC 方法内启动异步任务。", method));
                valid = false;
            }
            if (method.HasGenericParameters || method.DeclaringType.HasGenericParameters)
            {
                diagnostics.Add(Error("NETRPC102",
                    $"{method.FullName} 暂不支持泛型 RPC。", method));
                valid = false;
            }
            if (method.IsVirtual)
            {
                diagnostics.Add(Error("NETRPC103",
                    $"{method.FullName} 暂不支持 virtual/override RPC。", method));
                valid = false;
            }
            foreach (ParameterDefinition parameter in method.Parameters)
            {
                if (parameter.ParameterType.IsByReference || parameter.IsOut)
                {
                    diagnostics.Add(Error("NETRPC104",
                        $"{method.FullName} 不支持 ref/out 参数。", method));
                    valid = false;
                }
                else if (!IsSupportedRpcType(parameter.ParameterType))
                {
                    diagnostics.Add(Error("NETRPC105",
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
#endif
