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
    /// <summary>自动检测并同步当前网络物体上所有带 WebSyncVar 的字段。</summary>
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkSyncVars : WebNetworkBehaviour
    {
        [SerializeField, Min(0.02f)] float syncInterval = 0.1f;

        readonly Dictionary<ulong, Binding> bindings = new Dictionary<ulong, Binding>();
        float nextSyncTime;

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
        }

        void Awake()
        {
            BuildBindings();
        }

        void OnEnable()
        {
            WebNetworkManager.SyncVarReceived += OnSyncVarReceived;
            WebNetworkManager.AuthorityChanged += OnAuthorityChanged;
        }

        void OnDisable()
        {
            WebNetworkManager.SyncVarReceived -= OnSyncVarReceived;
            WebNetworkManager.AuthorityChanged -= OnAuthorityChanged;
        }

        void Update()
        {
            if (!HasAuthority || NetId == 0 || !LobbyWebNet.IsConnected || Time.unscaledTime < nextSyncTime)
                return;
            nextSyncTime = Time.unscaledTime + syncInterval;

            foreach (Binding binding in bindings.Values)
            {
                object value = binding.Field.GetValue(binding.Target);
                if (binding.Initialized && ValuesEqual(binding.LastValue, value)) continue;
                if (!TrySerialize(value, binding.Field.FieldType, out uint valueType, out byte[] bytes)) continue;

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
                        Value = ByteString.CopyFrom(bytes),
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
                        bindings.Add(key, new Binding { Target = component, Field = field,
                            Attribute = attribute, BehaviourId = behaviourId });
                    }
                }
            }
        }

        void OnSyncVarReceived(NetworkSyncVarData data)
        {
            if (HasAuthority || data.ObjectId != NetId ||
                !bindings.TryGetValue(MakeKey(data.BehaviourId, data.FieldId), out Binding binding) ||
                data.Sequence <= binding.ReceiveSequence)
                return;

            if (!TryDeserialize(data.Value.ToByteArray(), data.ValueType, binding.Field.FieldType, out object newValue))
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

        static void InvokeHook(Binding binding, object oldValue, object newValue)
        {
            if (string.IsNullOrWhiteSpace(binding.Attribute.Hook)) return;
            MethodInfo hook = binding.Target.GetType().GetMethod(binding.Attribute.Hook,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (hook == null) return;
            ParameterInfo[] parameters = hook.GetParameters();
            if (parameters.Length == 2) hook.Invoke(binding.Target, new[] { oldValue, newValue });
            else if (parameters.Length == 1) hook.Invoke(binding.Target, new[] { newValue });
            else if (parameters.Length == 0) hook.Invoke(binding.Target, null);
        }

        static bool TrySerialize(object value, Type type, out uint valueType, out byte[] bytes)
        {
            valueType = 0; bytes = null;
            if (value == null && type != typeof(string) && type != typeof(byte[])) return false;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
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
            else if (type.IsEnum) { valueType = 13; writer.Write(Convert.ToInt64(value)); }
            else if (type == typeof(Vector2)) { valueType = 14; Vector2 v = (Vector2)value; writer.Write(v.x); writer.Write(v.y); }
            else if (type == typeof(Vector3)) { valueType = 15; Vector3 v = (Vector3)value; writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            else if (type == typeof(Quaternion)) { valueType = 16; Quaternion q = (Quaternion)value; writer.Write(q.x); writer.Write(q.y); writer.Write(q.z); writer.Write(q.w); }
            else if (type == typeof(byte[])) { valueType = 17; writer.Write((byte[])value ?? Array.Empty<byte>()); }
            else return false;
            bytes = stream.ToArray();
            return true;
        }

        static bool TryDeserialize(byte[] bytes, uint valueType, Type targetType, out object value)
        {
            value = null;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes));
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
                return value != null && (targetType.IsInstanceOfType(value) || targetType.IsEnum);
            }
            catch { return false; }
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
