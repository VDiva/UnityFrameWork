using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameData;
using Google.Protobuf;
using UnityEngine;
using FrameWork.Net;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 标记由对象控制端发起、在房间内其他客户端的相同网络对象和组件上执行的方法。
    /// NetRpc Weaver 会在 Unity 编译后自动注入发送包装逻辑。
    /// 方法必须返回 void，参数仅支持 NetworkRpcDispatcher 中列出的基础网络类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class ClientRpcAttribute : Attribute
    {
        /// <summary>
        /// false：发起端立即执行，服务端只转发给其他客户端。
        /// true：发起端不立即执行，和其他客户端一起等待服务端回包后执行。
        /// </summary>
        public bool IncludeSender { get; set; }
    }

    /// <summary>客户端 RPC 的发送、参数序列化和远端反射调用入口。</summary>
    public static class NetworkRpcDispatcher
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
            public ClientRpcAttribute Attribute;
            public uint MethodId;
            public Func<MonoBehaviour, BinaryReader, bool> FastInvoker;
        }

        sealed class RpcTypeMetadata
        {
            public uint BehaviourId;
            public readonly Dictionary<string, List<MethodInfo>> ByName = new();
            public readonly Dictionary<uint, MethodInfo> ById = new();
        }

        const string FastInvokePrefix = "__NetRpcInvoke_";
        static readonly Dictionary<Type, RpcTypeMetadata> rpcTypes = new();
        static readonly Dictionary<MethodInfo, RpcMethodMetadata> rpcMethods = new();
        static readonly List<MonoBehaviour> invokeComponents = new(8);
        static MemoryStream serializeStream;
        static BinaryWriter serializeWriter;
        static ByteStringReaderStream deserializeStream;
        static BinaryReader deserializeReader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRpcCache()
        {
            rpcTypes.Clear();
            rpcMethods.Clear();
            serializeWriter?.Dispose();
            serializeWriter = null;
            serializeStream?.Dispose();
            serializeStream = null;
            deserializeReader?.Dispose();
            deserializeReader = null;
            deserializeStream?.Dispose();
            deserializeStream = null;
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
                    var attribute = method.GetCustomAttribute<ClientRpcAttribute>(true);
                    if (attribute == null)
                        continue;
                    uint methodId = GetMethodId(method);
                    Func<MonoBehaviour, BinaryReader, bool> fastInvoker = null;
                    MethodInfo stub = method.DeclaringType?.GetMethod(
                        $"{FastInvokePrefix}{methodId:X8}",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (stub != null)
                    {
                        fastInvoker = Delegate.CreateDelegate(
                            typeof(Func<MonoBehaviour, BinaryReader, bool>), stub, false)
                            as Func<MonoBehaviour, BinaryReader, bool>;
                    }
                    metadata = new RpcMethodMetadata
                    {
                        Method = method,
                        Parameters = method.GetParameters(),
                        Attribute = attribute,
                        MethodId = methodId,
                        FastInvoker = fastInvoker
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

        public static BinaryWriter BeginFastRelay(
            MonoBehaviour sender,
            uint methodId,
            int paramCount,
            bool includeSender,
            out uint behaviourId,
            out bool shouldExecuteLocally)
        {
            behaviourId = 0;
            uint netId = sender is NetworkBehaviour nb ? nb.NetId : (sender is NetworkIdentity ni ? ni.ObjectId : 0u);
            if (sender == null || netId == 0 || !GameNet.IsConnected)
            {
                shouldExecuteLocally = false;
                return null;
            }

            behaviourId = GetRpcType(sender.GetType()).BehaviourId;
            if (remoteInvokeDepth > 0)
            {
                if (netId == invokingObjectId && behaviourId == invokingBehaviourId && methodId == invokingMethodId)
                {
                    shouldExecuteLocally = true;
                    return null;
                }
                if (!invokingOnAuthority)
                {
                    shouldExecuteLocally = false;
                    return null;
                }
            }

            NetworkRoomManager roomManager = NetworkRoomManager.Instance;
            if (roomManager?.CurrentRoom != null &&
                roomManager.CurrentRoom.Members.Count == 1 &&
                roomManager.TryGetLocalRoomMember(out _))
            {
                shouldExecuteLocally = true;
                return null;
            }

            if (serializeStream == null)
            {
                serializeStream = new MemoryStream(128);
                serializeWriter = new BinaryWriter(serializeStream, System.Text.Encoding.UTF8, true);
            }
            serializeStream.SetLength(0);
            serializeStream.Position = 0;
            serializeWriter.Write(paramCount);
            shouldExecuteLocally = !includeSender;
            return serializeWriter;
        }

        public static void EndFastRelay(MonoBehaviour sender, uint behaviourId, uint methodId, bool includeSender)
        {
            if (serializeWriter == null || serializeStream == null) return;
            uint netId = sender is NetworkBehaviour nb ? nb.NetId : (sender is NetworkIdentity ni ? ni.ObjectId : 0u);
            if (netId == 0) return;

            serializeWriter.Flush();
            ByteString payload = ByteString.CopyFrom(serializeStream.GetBuffer(), 0, (int)serializeStream.Length);
            GameNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkRpc,
                NetworkRpc = new NetworkRpcData
                {
                    ObjectId = netId,
                    BehaviourId = behaviourId,
                    MethodId = methodId,
                    Arguments = payload,
                    IncludeSender = includeSender
                }
            });
        }

        public static void WriteBool(BinaryWriter w, bool v) => w.Write(v);
        public static void WriteByte(BinaryWriter w, byte v) => w.Write(v);
        public static void WriteSByte(BinaryWriter w, sbyte v) => w.Write(v);
        public static void WriteInt16(BinaryWriter w, short v) => w.Write(v);
        public static void WriteUInt16(BinaryWriter w, ushort v) => w.Write(v);
        public static void WriteInt32(BinaryWriter w, int v) => w.Write(v);
        public static void WriteUInt32(BinaryWriter w, uint v) => w.Write(v);
        public static void WriteInt64(BinaryWriter w, long v) => w.Write(v);
        public static void WriteUInt64(BinaryWriter w, ulong v) => w.Write(v);
        public static void WriteSingle(BinaryWriter w, float v) => w.Write(v);
        public static void WriteDouble(BinaryWriter w, double v) => w.Write(v);
        public static void WriteString(BinaryWriter w, string v) => w.Write(v ?? string.Empty);
        public static void WriteVector2(BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        public static void WriteVector3(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        public static void WriteQuaternion(BinaryWriter w, Quaternion v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); w.Write(v.w); }
        public static void WriteBytes(BinaryWriter w, byte[] v)
        {
            byte[] arr = v ?? Array.Empty<byte>();
            w.Write(arr.Length);
            w.Write(arr);
        }

        public static bool ReadBool(BinaryReader r) => r.ReadBoolean();
        public static byte ReadByte(BinaryReader r) => r.ReadByte();
        public static sbyte ReadSByte(BinaryReader r) => r.ReadSByte();
        public static short ReadInt16(BinaryReader r) => r.ReadInt16();
        public static ushort ReadUInt16(BinaryReader r) => r.ReadUInt16();
        public static int ReadInt32(BinaryReader r) => r.ReadInt32();
        public static uint ReadUInt32(BinaryReader r) => r.ReadUInt32();
        public static long ReadInt64(BinaryReader r) => r.ReadInt64();
        public static ulong ReadUInt64(BinaryReader r) => r.ReadUInt64();
        public static float ReadSingle(BinaryReader r) => r.ReadSingle();
        public static double ReadDouble(BinaryReader r) => r.ReadDouble();
        public static string ReadString(BinaryReader r) => r.ReadString();
        public static Vector2 ReadVector2(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());
        public static Vector3 ReadVector3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public static Quaternion ReadQuaternion(BinaryReader r) =>
            new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public static byte[] ReadBytes(BinaryReader r)
        {
            int length = r.ReadInt32();
            if (length < 0 || length > 64 * 1024)
                throw new InvalidDataException("Invalid RPC byte[] length.");
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("Truncated RPC byte[] payload.");
            return bytes;
        }

        internal static bool TryRelay(
            NetworkBehaviour sender,
            string methodName,
            object[] arguments)
        {
            return TryRelayComponent(sender, sender != null ? sender.NetId : 0u, methodName, arguments);
        }

        internal static bool TryRelay(
            NetworkIdentity sender,
            string methodName,
            object[] arguments)
        {
            return TryRelayComponent(sender, sender != null ? sender.ObjectId : 0u, methodName, arguments);
        }

        static bool TryRelayComponent(
            MonoBehaviour sender,
            uint netId,
            string methodName,
            object[] arguments)
        {
            if (sender == null || netId == 0 ||
                !GameNet.IsConnected || string.IsNullOrWhiteSpace(methodName))
            {
                return true;
            }

            MethodInfo method = FindRpcMethod(sender.GetType(), methodName, arguments);
            ClientRpcAttribute attribute = method == null ? null : rpcMethods[method].Attribute;
            if (method == null || attribute == null)
            {
                Debug.LogError($"[ClientRpc] 未找到带特性的方法：{sender.GetType().FullName}.{methodName}", sender);
                return true;
            }
            if (method.ReturnType != typeof(void))
            {
                Debug.LogError($"[ClientRpc] RPC 方法必须返回 void：{method.DeclaringType?.FullName}.{method.Name}", sender);
                return true;
            }
            uint behaviourId = GetRpcType(sender.GetType()).BehaviourId;
            uint methodId = rpcMethods[method].MethodId;
            if (remoteInvokeDepth > 0)
            {
                // 当前 RPC 的包装方法必须进入原方法体，不能把回包再次发回服务器。
                if (netId == invokingObjectId && behaviourId == invokingBehaviourId &&
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
            NetworkRoomManager roomManager = NetworkRoomManager.Instance;
            if (roomManager?.CurrentRoom != null &&
                roomManager.CurrentRoom.Members.Count == 1 &&
                roomManager.TryGetLocalRoomMember(out _))
            {
                return false;
            }

            if (!TrySerializeArguments(rpcMethods[method].Parameters, arguments, out ByteString payload))
            {
                Debug.LogError($"[ClientRpc] 参数不受支持：{method.DeclaringType?.FullName}.{method.Name}", sender);
                return true;
            }

            GameNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkRpc,
                NetworkRpc = new NetworkRpcData
                {
                    ObjectId = netId,
                    BehaviourId = behaviourId,
                    MethodId = methodId,
                    Arguments = payload,
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
                NetworkManager.Instance == null ||
                !NetworkManager.Instance.TryGetObject(data.ObjectId, out NetworkIdentity identity) ||
                identity == null)
            {
                return;
            }

            invokeComponents.Clear();
            identity.GetComponents(invokeComponents);
            for (int cIndex = 0; cIndex < invokeComponents.Count; cIndex++)
            {
                MonoBehaviour component = invokeComponents[cIndex];
                if (component == null)
                    continue;
                Type type = component.GetType();
                if (GetRpcType(type).BehaviourId != data.BehaviourId)
                    continue;

                MethodInfo method = FindRpcMethod(type, data.MethodId);
                if (method == null)
                {
                    Debug.LogWarning($"[ClientRpc] 远端找不到 RPC：behaviour={data.BehaviourId}, method={data.MethodId}", identity);
                    return;
                }

                RpcMethodMetadata meta = rpcMethods[method];
                object[] fallbackArguments = null;
                if (meta.FastInvoker == null)
                {
                    if (!TryDeserializeArguments(meta.Parameters, data.Arguments, out fallbackArguments))
                    {
                        Debug.LogWarning($"[ClientRpc] 无法解析 RPC 参数：{type.FullName}.{method.Name}", identity);
                        return;
                    }
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
                    invokingOnAuthority = component is NetworkBehaviour behaviour
                        ? behaviour.HasAuthority
                        : component is NetworkIdentity id && id.HasAuthority;

                    if (meta.FastInvoker != null)
                    {
                        BinaryReader reader = PrepareDeserializeReader(data.Arguments);
                        try
                        {
                            if (!meta.FastInvoker(component, reader) ||
                                reader.BaseStream.Position != reader.BaseStream.Length)
                                Debug.LogWarning($"[ClientRpc] 无法解析 RPC 参数：{type.FullName}.{method.Name}", identity);
                        }
                        catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException)
                        {
                            Debug.LogWarning($"[ClientRpc] 无法解析 RPC 参数：{type.FullName}.{method.Name}", identity);
                        }
                        finally
                        {
                            deserializeStream?.Reset(ByteString.Empty);
                        }
                    }
                    else
                    {
                        method.Invoke(component, fallbackArguments);
                    }
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, component);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, component);
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
                        !(parameters[i].ParameterType.IsEnum && parameters[i].ParameterType == arguments[i].GetType()))
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

        static bool TrySerializeArguments(ParameterInfo[] parameters, object[] arguments, out ByteString payload)
        {
            payload = null;
            arguments ??= Array.Empty<object>();
            if (parameters.Length != arguments.Length)
                return false;
            try
            {
                if (serializeStream == null)
                {
                    serializeStream = new MemoryStream(128);
                    serializeWriter = new BinaryWriter(serializeStream, System.Text.Encoding.UTF8, true);
                }
                serializeStream.SetLength(0);
                serializeStream.Position = 0;
                serializeWriter.Write(arguments.Length);
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (!TryWriteValue(serializeWriter, parameters[i].ParameterType, arguments[i]))
                        return false;
                }
                serializeWriter.Flush();
                payload = ByteString.CopyFrom(serializeStream.GetBuffer(), 0, (int)serializeStream.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static BinaryReader PrepareDeserializeReader(ByteString payload)
        {
            if (deserializeStream == null)
            {
                deserializeStream = new ByteStringReaderStream();
                deserializeReader = new BinaryReader(deserializeStream, System.Text.Encoding.UTF8, true);
            }
            deserializeStream.Reset(payload);
            return deserializeReader;
        }

        static bool TryDeserializeArguments(ParameterInfo[] parameters, ByteString payload, out object[] arguments)
        {
            arguments = null;
            try
            {
                BinaryReader reader = PrepareDeserializeReader(payload);
                int count = reader.ReadInt32();
                if (count != parameters.Length)
                    return false;
                arguments = count == 0 ? Array.Empty<object>() : new object[count];
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
            finally
            {
                deserializeStream?.Reset(ByteString.Empty);
            }
        }

        sealed class ByteStringReaderStream : Stream
        {
            ByteString data = ByteString.Empty;
            long position;
            public void Reset(ByteString value) { data = value ?? ByteString.Empty; position = 0; }
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => data.Length;
            public override long Position
            {
                get => position;
                set { if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value)); position = value; }
            }
            public override int ReadByte() => position < Length ? data[(int)position++] : -1;
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
                int size = (int)Math.Min(count, Length - position);
                data.Span.Slice((int)position, size).CopyTo(new Span<byte>(buffer, offset, size));
                position += size;
                return size;
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = (origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? position : Length) + offset;
                return position;
            }
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
            else if (type.IsEnum) writer.Write(Enum.GetUnderlyingType(type) == typeof(ulong) ? unchecked((long)Convert.ToUInt64(value)) : Convert.ToInt64(value));
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
