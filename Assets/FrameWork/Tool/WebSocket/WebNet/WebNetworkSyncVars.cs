using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GameData;
using Google.Protobuf;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>自动检测并同步当前网络物体上所有带 WebSyncVar 的字段。</summary>
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkSyncVars : WebNetworkBehaviour
    {
        [SerializeField, Min(0.02f)] float syncInterval = 0.1f;

        readonly Dictionary<ulong, Binding> bindings = new Dictionary<ulong, Binding>();
        float nextSyncTime;
        MemoryStream serializeStream;
        BinaryWriter serializeWriter;
        ByteStringInputStream deserializeStream;
        BinaryReader deserializeReader;

        sealed class Binding
        {
            public MonoBehaviour Target;
            public FieldInfo Field;
            public WebSyncVarAttribute Attribute;
            public uint BehaviourId;
            public object LastValue;
            public ulong SendSequence;
            public ulong ReceiveSequence;
            public bool Initialized;
            public MethodInfo Hook;
            public object[] HookArguments;
        }

        void Awake()
        {
            BuildBindings();
        }

        void OnEnable()
        {
            WebNetworkManager.AuthorityChanged += OnAuthorityChanged;
        }

        void OnDisable()
        {
            WebNetworkManager.AuthorityChanged -= OnAuthorityChanged;
        }

        void Update()
        {
            if (!HasAuthority || NetId == 0 || !LobbyWebNet.IsConnected || Time.unscaledTime < nextSyncTime)
                return;
            nextSyncTime = Time.unscaledTime + syncInterval;

            foreach (Binding binding in bindings.Values)
            {
                if (binding.Target == null) continue;
                object value = binding.Field.GetValue(binding.Target);
                if (binding.Initialized && ValuesEqual(binding.LastValue, value)) continue;
                if (!TrySerialize(value, binding.Field.FieldType, out uint valueType, out ByteString bytes)) continue;

                LobbyWebNet.Send(new Msg
                {
                    MsgType = ProtobufMsgType.Game,
                    GameMsgType = GameMsgType.UploadNetworkSyncVar,
                    NetworkSyncVar = new NetworkSyncVarData
                    {
                        ObjectId = NetId,
                        BehaviourId = binding.BehaviourId,
                        FieldId = binding.Attribute.FieldId,
                        ValueType = valueType,
                        Value = bytes,
                        Sequence = ++binding.SendSequence
                    }
                });
                binding.LastValue = CloneValue(value);
                binding.Initialized = true;
            }
        }

        void BuildBindings()
        {
            bindings.Clear();
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                Type type = component.GetType();
                uint behaviourId = StableHash(type.FullName ?? type.Name);
                for (Type current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        WebSyncVarAttribute attribute = field.GetCustomAttribute<WebSyncVarAttribute>();
                        if (attribute == null) continue;
                        ulong key = MakeKey(behaviourId, attribute.FieldId);
                        if (bindings.ContainsKey(key))
                        {
                            Debug.LogError($"[WebSyncVar] 重复字段 ID：{type.FullName} / {attribute.FieldId}", this);
                            continue;
                        }
                        var hook = ResolveHook(type, field.FieldType, attribute.Hook);
                        if (!string.IsNullOrWhiteSpace(attribute.Hook) && hook == null)
                            Debug.LogWarning($"[WebSyncVar] 找不到签名匹配的 Hook：{type.FullName}.{attribute.Hook}", this);
                        bindings.Add(key, new Binding { Target = component, Field = field,
                            Attribute = attribute, BehaviourId = behaviourId, Hook = hook,
                            HookArguments = hook == null || hook.GetParameters().Length == 0 ? null : new object[hook.GetParameters().Length] });
                    }
                }
            }
        }

        internal void OnSyncVarReceived(NetworkSyncVarData data)
        {
            if (HasAuthority || data.ObjectId != NetId ||
                !bindings.TryGetValue(MakeKey(data.BehaviourId, data.FieldId), out Binding binding) ||
                data.Sequence <= binding.ReceiveSequence)
                return;

            if (binding.Target == null || !TryDeserialize(data.Value, data.ValueType, binding.Field.FieldType, out object newValue))
                return;
            object oldValue = binding.Field.GetValue(binding.Target);
            binding.Field.SetValue(binding.Target, newValue);
            binding.LastValue = CloneValue(newValue);
            binding.Initialized = true;
            binding.ReceiveSequence = data.Sequence;
            InvokeHook(binding, oldValue, newValue);
        }

        void OnAuthorityChanged(WebNetworkIdentity changed)
        {
            if (changed != Identity) return;
            foreach (Binding binding in bindings.Values)
            {
                binding.SendSequence = 0;
                binding.ReceiveSequence = 0;
                binding.Initialized = false;
            }
            nextSyncTime = 0f;
        }

        private static MethodInfo ResolveHook(Type type, Type fieldType, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            // 在绑定时解析重载，优先双参、单参、无参及精确字段类型，兼容 object 等基类型参数。
            for (int count = 2; count >= 0; count--)
                for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
                {
                    MethodInfo best = null;
                    int bestScore = -1;
                    bool ambiguous = false;
                    foreach (var method in current.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != name || method.ContainsGenericParameters) continue;
                        var parameters = method.GetParameters();
                        if (parameters.Length != count) continue;
                        int score = 0;
                        bool compatible = true;
                        foreach (var parameter in parameters)
                        {
                            if (!parameter.ParameterType.IsAssignableFrom(fieldType)) { compatible = false; break; }
                            if (parameter.ParameterType == fieldType) score++;
                        }
                        if (!compatible) continue;
                        if (score > bestScore) { best = method; bestScore = score; ambiguous = false; }
                        else if (score == bestScore) ambiguous = true;
                    }
                    if (best != null) return ambiguous ? null : best;
                }
            return null;
        }

        static void InvokeHook(Binding binding, object oldValue, object newValue)
        {
            if (binding.Hook == null || binding.Target == null) return;
            var args = binding.HookArguments;
            if (args != null)
            {
                if (args.Length == 2) { args[0] = oldValue; args[1] = newValue; }
                else args[0] = newValue;
            }
            try { binding.Hook.Invoke(binding.Target, args); }
            catch (Exception error) { Debug.LogException(error); }
            finally { if (args != null) Array.Clear(args, 0, args.Length); }
        }

        void OnDestroy()
        {
            serializeWriter?.Dispose();
            serializeStream?.Dispose();
            deserializeReader?.Dispose();
            deserializeStream?.Dispose();
            bindings.Clear();
        }

        bool TrySerialize(object value, Type type, out uint valueType, out ByteString bytes)
        {
            valueType = 0; bytes = null;
            if (value == null && type != typeof(string) && type != typeof(byte[])) return false;
            if (type == typeof(byte[]))
            {
                valueType = 17;
                bytes = ByteString.CopyFrom((byte[])value ?? Array.Empty<byte>());
                return true;
            }
            if (serializeStream == null)
            {
                serializeStream = new MemoryStream(64);
                serializeWriter = new BinaryWriter(serializeStream, Encoding.UTF8, true);
            }
            var stream = serializeStream;
            var writer = serializeWriter;
            stream.SetLength(0);
            stream.Position = 0;
            if (type == typeof(bool)) { valueType = 1; writer.Write((bool)value); }
            else if (type == typeof(byte)) { valueType = 2; writer.Write((byte)value); }
            else if (type == typeof(sbyte)) { valueType = 3; writer.Write((sbyte)value); }
            else if (type == typeof(short)) { valueType = 4; writer.Write((short)value); }
            else if (type == typeof(ushort)) { valueType = 5; writer.Write((ushort)value); }
            else if (type == typeof(int)) { valueType = 6; writer.Write((int)value); }
            else if (type == typeof(uint)) { valueType = 7; writer.Write((uint)value); }
            else if (type == typeof(long)) { valueType = 8; writer.Write((long)value); }
            else if (type == typeof(ulong)) { valueType = 9; writer.Write((ulong)value); }
            else if (type == typeof(float)) { valueType = 10; writer.Write((float)value); }
            else if (type == typeof(double)) { valueType = 11; writer.Write((double)value); }
            else if (type == typeof(string)) { valueType = 12; writer.Write((string)value ?? string.Empty); }
            else if (type.IsEnum) { valueType = 13; writer.Write(Enum.GetUnderlyingType(type) == typeof(ulong) ? unchecked((long)Convert.ToUInt64(value)) : Convert.ToInt64(value)); }
            else if (type == typeof(Vector2)) { valueType = 14; Vector2 v = (Vector2)value; writer.Write(v.x); writer.Write(v.y); }
            else if (type == typeof(Vector3)) { valueType = 15; Vector3 v = (Vector3)value; writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            else if (type == typeof(Quaternion)) { valueType = 16; Quaternion q = (Quaternion)value; writer.Write(q.x); writer.Write(q.y); writer.Write(q.z); writer.Write(q.w); }
            else if (type == typeof(byte[])) { valueType = 17; writer.Write((byte[])value ?? Array.Empty<byte>()); }
            else return false;
            writer.Flush();
            bytes = ByteString.CopyFrom(stream.GetBuffer(), 0, (int)stream.Length);
            return true;
        }

        bool TryDeserialize(ByteString bytes, uint valueType, Type targetType, out object value)
        {
            value = null;
            try
            {
                if (deserializeStream == null)
                {
                    deserializeStream = new ByteStringInputStream();
                    deserializeReader = new BinaryReader(deserializeStream, Encoding.UTF8, true);
                }
                deserializeStream.Reset(bytes);
                var reader = deserializeReader;
                switch (valueType)
                {
                    case 1: value = reader.ReadBoolean(); break; case 2: value = reader.ReadByte(); break;
                    case 3: value = reader.ReadSByte(); break; case 4: value = reader.ReadInt16(); break;
                    case 5: value = reader.ReadUInt16(); break; case 6: value = reader.ReadInt32(); break;
                    case 7: value = reader.ReadUInt32(); break; case 8: value = reader.ReadInt64(); break;
                    case 9: value = reader.ReadUInt64(); break; case 10: value = reader.ReadSingle(); break;
                    case 11: value = reader.ReadDouble(); break; case 12: value = reader.ReadString(); break;
                    case 13: value = Enum.ToObject(targetType, reader.ReadInt64()); break;
                    case 14: value = new Vector2(reader.ReadSingle(), reader.ReadSingle()); break;
                    case 15: value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
                    case 16: value = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); break;
                    case 17: value = reader.ReadBytes((int)reader.BaseStream.Length); break;
                    default: return false;
                }
                return reader.BaseStream.Position == reader.BaseStream.Length && value != null && targetType.IsInstanceOfType(value);
            }
            catch { return false; }
            finally { deserializeStream?.Reset(ByteString.Empty); }
        }

        /// <summary>直接读取不可变 ByteString，避免每包复制到新的 byte[]。</summary>
        sealed class ByteStringInputStream : Stream
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

        static bool ValuesEqual(object a, object b)
        {
            if (a is byte[] aa && b is byte[] bb)
            {
                if (aa.Length != bb.Length) return false;
                for (int i = 0; i < aa.Length; i++) if (aa[i] != bb[i]) return false;
                return true;
            }
            return Equals(a, b);
        }

        static object CloneValue(object value) => value is byte[] bytes ? bytes.Clone() : value;
        static ulong MakeKey(uint behaviourId, uint fieldId) => ((ulong)behaviourId << 32) | fieldId;
        static uint StableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char c in value) { hash ^= c; hash *= prime; }
            return hash == 0 ? 1u : hash;
        }
    }
}
