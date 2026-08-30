using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameData;
using Google.Protobuf;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// 标记由对象控制端发起、在房间内其他客户端的相同网络对象和组件上执行的方法。
    /// WebRpc Weaver 会在 Unity 编译后自动注入发送包装逻辑。
    /// 方法必须返回 void，参数仅支持 WebNetworkRpcDispatcher 中列出的基础网络类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class WebClientRpcAttribute : Attribute
    {
        /// <summary>
        /// false：发起端立即执行，服务端只转发给其他客户端。
        /// true：发起端不立即执行，和其他客户端一起等待服务端回包后执行。
        /// </summary>
        public bool IncludeSender { get; set; }
    }

    /// <summary>客户端 RPC 的发送、参数序列化和远端反射调用入口。</summary>
    public static class WebNetworkRpcDispatcher
    {
        static int remoteInvokeDepth;
        static uint invokingObjectId;
        static uint invokingBehaviourId;
        static uint invokingMethodId;
        static bool invokingOnAuthority;
        // 只缓存类型元数据，不持有场景对象；继承链仅在类型首次使用时扫描。
        sealed class RpcMethodMetadata
        {
            public MethodInfo Method;
            public ParameterInfo[] Parameters;
            public WebClientRpcAttribute Attribute;
            public uint MethodId;
        }

        sealed class RpcTypeMetadata
        {
            public uint BehaviourId;
            public readonly Dictionary<string, List<MethodInfo>> ByName = new();
            public readonly Dictionary<uint, MethodInfo> ById = new();
        }

        static readonly Dictionary<Type, RpcTypeMetadata> rpcTypes = new();
        static readonly Dictionary<MethodInfo, RpcMethodMetadata> rpcMethods = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRpcCache()
        {
            rpcTypes.Clear();
            rpcMethods.Clear();
            remoteInvokeDepth = 0;
            invokingObjectId = invokingBehaviourId = invokingMethodId = 0;
            invokingOnAuthority = false;
        }

        static RpcTypeMetadata GetRpcType(Type type)
        {
            if (rpcTypes.TryGetValue(type, out var cached))
                return cached;

            cached = new RpcTypeMetadata { BehaviourId = StableHash(type.FullName ?? type.Name) };
            foreach (MethodInfo method in GetInstanceMethods(type))
            {
                if (!rpcMethods.TryGetValue(method, out var metadata))
                {
                    var attribute = method.GetCustomAttribute<WebClientRpcAttribute>(true);
                    if (attribute == null)
                        continue;
                    metadata = new RpcMethodMetadata
                    {
                        Method = method,
                        Parameters = method.GetParameters(),
                        Attribute = attribute,
                        MethodId = GetMethodId(method)
                    };
                    rpcMethods.Add(method, metadata);
                }
                if (!cached.ByName.TryGetValue(method.Name, out var overloads))
                {
                    overloads = new List<MethodInfo>();
                    cached.ByName.Add(method.Name, overloads);
                }
                overloads.Add(method);
                // 保留原查找顺序（派生类优先），方法 ID 和网络协议不变。
                if (!cached.ById.ContainsKey(metadata.MethodId))
                    cached.ById.Add(metadata.MethodId, method);
            }
            rpcTypes.Add(type, cached);
            return cached;
        }

        internal static bool TryRelay(
            WebNetworkBehaviour sender,
            string methodName,
            object[] arguments)
        {
            if (sender == null|| sender.NetId == 0 ||
                !LobbyWebNet.IsConnected || string.IsNullOrWhiteSpace(methodName))
            {
                return true;
            }

            MethodInfo method = FindRpcMethod(sender.GetType(), methodName, arguments);
            WebClientRpcAttribute attribute = method == null ? null : rpcMethods[method].Attribute;
            if (method == null || attribute == null)
            {
                Debug.LogError($"[WebClientRpc] 未找到带特性的方法：{sender.GetType().FullName}.{methodName}", sender);
                return true;
            }
            if (method.ReturnType != typeof(void))
            {
                Debug.LogError($"[WebClientRpc] RPC 方法必须返回 void：{method.DeclaringType?.FullName}.{method.Name}", sender);
                return true;
            }
            uint behaviourId = GetRpcType(sender.GetType()).BehaviourId;
            uint methodId = rpcMethods[method].MethodId;
            if (remoteInvokeDepth > 0)
            {
                // 当前 RPC 的包装方法必须进入原方法体，不能把回包再次发回服务器。
                if (sender.NetId == invokingObjectId && behaviourId == invokingBehaviourId &&
                    methodId == invokingMethodId)
                {
                    return false;
                }

                // 嵌套 RPC 是一次新的广播。只有外层 RPC 对象的权威端负责发送，
                // 其他客户端等待这次广播，避免先本地执行、收到回包后又执行一次。
                if (!invokingOnAuthority)
                    return true;
            }

            // 房间中只有本地玩家时没有任何远端接收者。直接进入用户方法体，
            // IncludeSender=true 也无需为了回到自己而经过一次服务器转发。
            WebNetworkRoomManager roomManager = WebNetworkRoomManager.Instance;
            if (roomManager?.CurrentRoom != null &&
                roomManager.CurrentRoom.Members.Count == 1 &&
                roomManager.TryGetLocalRoomMember(out _))
            {
                return false;
            }

            if (!TrySerializeArguments(rpcMethods[method].Parameters, arguments, out byte[] payload))
            {
                Debug.LogError($"[WebClientRpc] 参数不受支持：{method.DeclaringType?.FullName}.{method.Name}", sender);
                return true;
            }

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkRpc,
                NetworkRpc = new NetworkRpcData
                {
                    ObjectId = sender.NetId,
                    BehaviourId = behaviourId,
                    MethodId = methodId,
                    Arguments = ByteString.CopyFrom(payload),
                    IncludeSender = attribute.IncludeSender
                }
            });

            // false：包装方法继续执行本地方法体，服务端排除发送者。
            // true：包装方法先返回，等待服务端把 RPC 回发后再执行。
            return attribute.IncludeSender;
        }

        public static void Invoke(NetworkRpcData data)
        {
            if (data == null || data.ObjectId == 0 ||
                WebNetworkManager.Instance == null ||
                !WebNetworkManager.Instance.TryGetObject(data.ObjectId, out WebNetworkIdentity identity) ||
                identity == null)
            {
                return;
            }

            foreach (MonoBehaviour component in identity.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                    continue;
                Type type = component.GetType();
                if (GetRpcType(type).BehaviourId != data.BehaviourId)
                    continue;

                MethodInfo method = FindRpcMethod(type, data.MethodId);
                if (method == null)
                {
                    Debug.LogWarning($"[WebClientRpc] 远端找不到 RPC：behaviour={data.BehaviourId}, method={data.MethodId}", identity);
                    return;
                }
                if (!TryDeserializeArguments(rpcMethods[method].Parameters, data.Arguments.ToByteArray(), out object[] arguments))
                {
                    Debug.LogWarning($"[WebClientRpc] 无法解析 RPC 参数：{type.FullName}.{method.Name}", identity);
                    return;
                }

                uint previousObjectId = invokingObjectId;
                uint previousBehaviourId = invokingBehaviourId;
                uint previousMethodId = invokingMethodId;
                bool previousInvokingOnAuthority = invokingOnAuthority;
                try
                {
                    remoteInvokeDepth++;
                    invokingObjectId = data.ObjectId;
                    invokingBehaviourId = data.BehaviourId;
                    invokingMethodId = data.MethodId;
                    invokingOnAuthority = component is WebNetworkBehaviour behaviour && behaviour.HasAuthority;
                    method.Invoke(component, arguments);
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, component);
                }
                finally
                {
                    invokingObjectId = previousObjectId;
                    invokingBehaviourId = previousBehaviourId;
                    invokingMethodId = previousMethodId;
                    invokingOnAuthority = previousInvokingOnAuthority;
                    remoteInvokeDepth--;
                }
                return;
            }
        }

        static MethodInfo FindRpcMethod(Type type, string methodName, object[] arguments)
        {
            arguments ??= Array.Empty<object>();
            if (!GetRpcType(type).ByName.TryGetValue(methodName, out var overloads))
                return null;
            foreach (MethodInfo method in overloads)
            {
                ParameterInfo[] parameters = rpcMethods[method].Parameters;
                if (parameters.Length != arguments.Length)
                    continue;
                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (arguments[i] != null && !parameters[i].ParameterType.IsInstanceOfType(arguments[i]) &&
                        !(parameters[i].ParameterType.IsEnum && arguments[i].GetType().IsEnum))
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    return method;
            }
            return null;
        }

        static MethodInfo FindRpcMethod(Type type, uint methodId)
        {
            GetRpcType(type).ById.TryGetValue(methodId, out var method);
            return method;
        }
        static IEnumerable<MethodInfo> GetInstanceMethods(Type type)
        {
            for (Type current = type;
                 current != null && current != typeof(MonoBehaviour);
                 current = current.BaseType)
            {
                foreach (MethodInfo method in current.GetMethods(
                             BindingFlags.Instance | BindingFlags.Public |
                             BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    yield return method;
                }
            }
        }

        static uint GetMethodId(MethodInfo method)
        {
            var signature = new System.Text.StringBuilder();
            signature.Append(method.DeclaringType?.FullName).Append('.').Append(method.Name).Append('(');
            foreach (ParameterInfo parameter in method.GetParameters())
                signature.Append(parameter.ParameterType.FullName).Append(';');
            signature.Append(')');
            return StableHash(signature.ToString());
        }

        static bool TrySerializeArguments(ParameterInfo[] parameters, object[] arguments, out byte[] payload)
        {
            payload = null;
            arguments ??= Array.Empty<object>();
            if (parameters.Length != arguments.Length)
                return false;
            try
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream);
                writer.Write(arguments.Length);
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (!TryWriteValue(writer, parameters[i].ParameterType, arguments[i]))
                        return false;
                }
                payload = stream.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TryDeserializeArguments(ParameterInfo[] parameters, byte[] payload, out object[] arguments)
        {
            arguments = null;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(payload ?? Array.Empty<byte>()));
                int count = reader.ReadInt32();
                if (count != parameters.Length)
                    return false;
                arguments = new object[count];
                for (int i = 0; i < count; i++)
                    if (!TryReadValue(reader, parameters[i].ParameterType, out arguments[i]))
                        return false;
                return reader.BaseStream.Position == reader.BaseStream.Length;
            }
            catch
            {
                arguments = null;
                return false;
            }
        }

        static bool TryWriteValue(BinaryWriter writer, Type type, object value)
        {
            if (type == typeof(bool)) writer.Write((bool)value);
            else if (type == typeof(byte)) writer.Write((byte)value);
            else if (type == typeof(sbyte)) writer.Write((sbyte)value);
            else if (type == typeof(short)) writer.Write((short)value);
            else if (type == typeof(ushort)) writer.Write((ushort)value);
            else if (type == typeof(int)) writer.Write((int)value);
            else if (type == typeof(uint)) writer.Write((uint)value);
            else if (type == typeof(long)) writer.Write((long)value);
            else if (type == typeof(ulong)) writer.Write((ulong)value);
            else if (type == typeof(float)) writer.Write((float)value);
            else if (type == typeof(double)) writer.Write((double)value);
            else if (type == typeof(string)) writer.Write((string)value ?? string.Empty);
            else if (type.IsEnum) writer.Write(Convert.ToInt64(value));
            else if (type == typeof(Vector2))
            {
                Vector2 vector = (Vector2)value; writer.Write(vector.x); writer.Write(vector.y);
            }
            else if (type == typeof(Vector3))
            {
                Vector3 vector = (Vector3)value; writer.Write(vector.x); writer.Write(vector.y); writer.Write(vector.z);
            }
            else if (type == typeof(Quaternion))
            {
                Quaternion valueQuaternion = (Quaternion)value;
                writer.Write(valueQuaternion.x); writer.Write(valueQuaternion.y);
                writer.Write(valueQuaternion.z); writer.Write(valueQuaternion.w);
            }
            else if (type == typeof(byte[]))
            {
                byte[] bytes = (byte[])value ?? Array.Empty<byte>();
                writer.Write(bytes.Length); writer.Write(bytes);
            }
            else return false;
            return true;
        }

        static bool TryReadValue(BinaryReader reader, Type type, out object value)
        {
            value = null;
            if (type == typeof(bool)) value = reader.ReadBoolean();
            else if (type == typeof(byte)) value = reader.ReadByte();
            else if (type == typeof(sbyte)) value = reader.ReadSByte();
            else if (type == typeof(short)) value = reader.ReadInt16();
            else if (type == typeof(ushort)) value = reader.ReadUInt16();
            else if (type == typeof(int)) value = reader.ReadInt32();
            else if (type == typeof(uint)) value = reader.ReadUInt32();
            else if (type == typeof(long)) value = reader.ReadInt64();
            else if (type == typeof(ulong)) value = reader.ReadUInt64();
            else if (type == typeof(float)) value = reader.ReadSingle();
            else if (type == typeof(double)) value = reader.ReadDouble();
            else if (type == typeof(string)) value = reader.ReadString();
            else if (type.IsEnum) value = Enum.ToObject(type, reader.ReadInt64());
            else if (type == typeof(Vector2)) value = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(Vector3)) value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(Quaternion))
                value = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(byte[]))
            {
                int length = reader.ReadInt32();
                if (length < 0 || length > 64 * 1024)
                    return false;
                value = reader.ReadBytes(length);
                if (((byte[])value).Length != length)
                    return false;
            }
            else return false;
            return true;
        }

        static uint StableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }
            return hash == 0 ? 1u : hash;
        }
    }
}
