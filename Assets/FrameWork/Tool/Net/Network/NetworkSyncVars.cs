using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GameData;
using Google.Protobuf;
using UnityEngine;
using FrameWork.Net;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 自动检测并同步当前网络物体上所有带 [SyncVar] 的字段。
    /// 优先绑定 NetRpc Weaver 生成的强类型委托实现 0 装箱轮询与反序列化，并将同一帧多字段变更合并单包发送。
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class NetworkSyncVars : NetworkBehaviour
    {
        const string SyncGetPrefix = "__NetSyncGet_";
        const string SyncSetPrefix = "__NetSyncSet_";
        static readonly List<MonoBehaviour> tempBehaviours = new List<MonoBehaviour>(8);

        [SerializeField, Min(0.02f)] float syncInterval = 0.1f;

        readonly Dictionary<ulong, SyncBinding> bindings = new Dictionary<ulong, SyncBinding>();
        readonly List<SyncBinding> bindingList = new List<SyncBinding>();
        readonly List<NetworkSyncVarData> pendingBatch = new List<NetworkSyncVarData>(4);
        internal int tickIndex = -1;
        float nextSyncTime;
        MemoryStream serializeStream;
        BinaryWriter serializeWriter;
        ByteStringInputStream deserializeStream;
        BinaryReader deserializeReader;

        abstract class SyncBinding
        {
            public MonoBehaviour Target;
            public FieldInfo Field;
            public SyncVarAttribute Attribute;
            public uint BehaviourId;
            public uint WireValueType;
            public ulong SendSequence;
            public ulong ReceiveSequence;
            public bool Initialized;

            public abstract bool PollAndSerialize(NetworkSyncVars owner, out ByteString bytes);
            public abstract void ApplyReceived(NetworkSyncVars owner, ByteString bytes, uint valueType, ulong sequence);
            public virtual void Reset()
            {
                SendSequence = 0;
                ReceiveSequence = 0;
                Initialized = false;
            }
        }

        sealed class TypedBinding<T> : SyncBinding
        {
            readonly Func<T> fastGetter;
            readonly Action<T> fastSetter;
            readonly Action<T, BinaryWriter> writeFn;
            readonly Func<BinaryReader, T> readFn;
            readonly Func<T, T, bool> equalsFn;
            readonly Func<T, T> cloneFn;
            readonly Action<T, T> hook2;
            readonly Action<T> hook1;
            readonly Action hook0;
            readonly MethodInfo fallbackHook;
            readonly object[] fallbackHookArgs;
            T lastValue;

            public TypedBinding(
                MonoBehaviour target,
                FieldInfo field,
                MethodInfo getterMethod,
                MethodInfo setterMethod,
                MethodInfo hookMethod,
                uint wireValueType,
                Action<T, BinaryWriter> writeFn,
                Func<BinaryReader, T> readFn,
                Func<T, T, bool> equalsFn = null,
                Func<T, T> cloneFn = null)
            {
                Target = target;
                Field = field;
                WireValueType = wireValueType;
                this.writeFn = writeFn;
                this.readFn = readFn;
                this.equalsFn = equalsFn ?? EqualityComparer<T>.Default.Equals;
                this.cloneFn = cloneFn;

                if (getterMethod != null)
                    fastGetter = Delegate.CreateDelegate(typeof(Func<T>), target, getterMethod, false) as Func<T>;
                if (setterMethod != null)
                    fastSetter = Delegate.CreateDelegate(typeof(Action<T>), target, setterMethod, false) as Action<T>;

                if (hookMethod != null)
                {
                    ParameterInfo[] ps = hookMethod.GetParameters();
                    if (ps.Length == 0)
                        hook0 = Delegate.CreateDelegate(typeof(Action), target, hookMethod, false) as Action;
                    else if (ps.Length == 1 && ps[0].ParameterType == typeof(T))
                        hook1 = Delegate.CreateDelegate(typeof(Action<T>), target, hookMethod, false) as Action<T>;
                    else if (ps.Length == 2 && ps[0].ParameterType == typeof(T) && ps[1].ParameterType == typeof(T))
                        hook2 = Delegate.CreateDelegate(typeof(Action<T, T>), target, hookMethod, false) as Action<T, T>;

                    if (hook0 == null && hook1 == null && hook2 == null)
                    {
                        fallbackHook = hookMethod;
                        fallbackHookArgs = ps.Length == 0 ? null : new object[ps.Length];
                    }
                }
            }

            T ReadField() => fastGetter != null ? fastGetter() : (T)Field.GetValue(Target);
            void WriteField(T value)
            {
                if (fastSetter != null) fastSetter(value);
                else Field.SetValue(Target, value);
            }

            public override bool PollAndSerialize(NetworkSyncVars owner, out ByteString bytes)
            {
                bytes = null;
                T current = ReadField();
                if (current == null && typeof(T) != typeof(string) && typeof(T) != typeof(byte[]))
                    return false;
                if (Initialized && equalsFn(lastValue, current))
                    return false;

                if (WireValueType == 17)
                {
                    byte[] raw = (byte[])(object)current ?? Array.Empty<byte>();
                    bytes = ByteString.CopyFrom(raw);
                }
                else
                {
                    BinaryWriter writer = owner.PrepareWriter();
                    writeFn(current, writer);
                    writer.Flush();
                    bytes = ByteString.CopyFrom(owner.serializeStream.GetBuffer(), 0, (int)owner.serializeStream.Length);
                }

                lastValue = cloneFn != null ? cloneFn(current) : current;
                Initialized = true;
                return true;
            }

            public override void ApplyReceived(NetworkSyncVars owner, ByteString bytes, uint valueType, ulong sequence)
            {
                if (valueType != WireValueType) return;
                T newValue;
                try
                {
                    BinaryReader reader = owner.PrepareReader(bytes);
                    newValue = readFn(reader);
                    if (reader.BaseStream.Position != reader.BaseStream.Length)
                        return;
                }
                catch
                {
                    return;
                }
                finally
                {
                    owner.deserializeStream?.Reset(ByteString.Empty);
                }

                T oldValue = ReadField();
                WriteField(newValue);
                lastValue = cloneFn != null ? cloneFn(newValue) : newValue;
                Initialized = true;
                ReceiveSequence = sequence;
                InvokeTypedHook(oldValue, newValue);
            }

            void InvokeTypedHook(T oldValue, T newValue)
            {
                try
                {
                    if (hook2 != null) hook2(oldValue, newValue);
                    else if (hook1 != null) hook1(newValue);
                    else if (hook0 != null) hook0();
                    else if (fallbackHook != null)
                    {
                        if (fallbackHookArgs != null)
                        {
                            if (fallbackHookArgs.Length == 2) { fallbackHookArgs[0] = oldValue; fallbackHookArgs[1] = newValue; }
                            else fallbackHookArgs[0] = newValue;
                        }
                        fallbackHook.Invoke(Target, fallbackHookArgs);
                    }
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
                finally
                {
                    if (fallbackHookArgs != null)
                        Array.Clear(fallbackHookArgs, 0, fallbackHookArgs.Length);
                }
            }

            public override void Reset()
            {
                base.Reset();
                lastValue = default;
            }
        }

        sealed class EnumBinding : SyncBinding
        {
            readonly Func<long> fastGetter;
            readonly Action<long> fastSetter;
            readonly bool isUnsigned64;
            readonly MethodInfo hookMethod;
            readonly object[] hookArgs;
            long lastRaw;

            public EnumBinding(MonoBehaviour target, FieldInfo field, MethodInfo getterMethod, MethodInfo setterMethod, MethodInfo hook)
            {
                Target = target;
                Field = field;
                WireValueType = 13;
                isUnsigned64 = Enum.GetUnderlyingType(field.FieldType) == typeof(ulong);
                if (getterMethod != null)
                    fastGetter = Delegate.CreateDelegate(typeof(Func<long>), target, getterMethod, false) as Func<long>;
                if (setterMethod != null)
                    fastSetter = Delegate.CreateDelegate(typeof(Action<long>), target, setterMethod, false) as Action<long>;
                hookMethod = hook;
                if (hook != null)
                {
                    int len = hook.GetParameters().Length;
                    hookArgs = len == 0 ? null : new object[len];
                }
            }

            long ReadRaw()
            {
                if (fastGetter != null) return fastGetter();
                object boxed = Field.GetValue(Target);
                return isUnsigned64 ? unchecked((long)Convert.ToUInt64(boxed)) : Convert.ToInt64(boxed);
            }

            void WriteRaw(long raw)
            {
                if (fastSetter != null) fastSetter(raw);
                else Field.SetValue(Target, Enum.ToObject(Field.FieldType, raw));
            }

            public override bool PollAndSerialize(NetworkSyncVars owner, out ByteString bytes)
            {
                bytes = null;
                long current = ReadRaw();
                if (Initialized && current == lastRaw)
                    return false;

                BinaryWriter writer = owner.PrepareWriter();
                writer.Write(current);
                writer.Flush();
                bytes = ByteString.CopyFrom(owner.serializeStream.GetBuffer(), 0, (int)owner.serializeStream.Length);
                lastRaw = current;
                Initialized = true;
                return true;
            }

            public override void ApplyReceived(NetworkSyncVars owner, ByteString bytes, uint valueType, ulong sequence)
            {
                if (valueType != 13) return;
                long newRaw;
                try
                {
                    BinaryReader reader = owner.PrepareReader(bytes);
                    newRaw = reader.ReadInt64();
                    if (reader.BaseStream.Position != reader.BaseStream.Length)
                        return;
                }
                catch
                {
                    return;
                }
                finally
                {
                    owner.deserializeStream?.Reset(ByteString.Empty);
                }

                object oldBoxed = hookMethod != null && hookArgs != null && hookArgs.Length == 2
                    ? Enum.ToObject(Field.FieldType, ReadRaw())
                    : null;
                WriteRaw(newRaw);
                lastRaw = newRaw;
                Initialized = true;
                ReceiveSequence = sequence;

                if (hookMethod != null)
                {
                    try
                    {
                        if (hookArgs != null)
                        {
                            object newBoxed = Enum.ToObject(Field.FieldType, newRaw);
                            if (hookArgs.Length == 2) { hookArgs[0] = oldBoxed; hookArgs[1] = newBoxed; }
                            else hookArgs[0] = newBoxed;
                        }
                        hookMethod.Invoke(Target, hookArgs);
                    }
                    catch (Exception error) { Debug.LogException(error); }
                    finally { if (hookArgs != null) Array.Clear(hookArgs, 0, hookArgs.Length); }
                }
            }

            public override void Reset()
            {
                base.Reset();
                lastRaw = 0;
            }
        }

        void Awake()
        {
            BuildBindings();
        }

        void OnEnable()
        {
            NetworkManager.AuthorityChanged += OnAuthorityChanged;
            NetworkManager.RegisterTickableSyncVars(this);
        }

        void OnDisable()
        {
            NetworkManager.AuthorityChanged -= OnAuthorityChanged;
            NetworkManager.UnregisterTickableSyncVars(this);
        }

        internal void TickAuthority(float now)
        {
            if (!HasAuthority || NetId == 0 || !GameNet.IsConnected || now < nextSyncTime)
                return;
            nextSyncTime = now + syncInterval;

            pendingBatch.Clear();
            uint netId = NetId;
            for (int i = 0; i < bindingList.Count; i++)
            {
                SyncBinding binding = bindingList[i];
                if (binding.Target == null) continue;
                if (!binding.PollAndSerialize(this, out ByteString bytes)) continue;

                pendingBatch.Add(new NetworkSyncVarData
                {
                    ObjectId = netId,
                    BehaviourId = binding.BehaviourId,
                    FieldId = binding.Attribute.FieldId,
                    ValueType = binding.WireValueType,
                    Value = bytes,
                    Sequence = ++binding.SendSequence
                });
            }

            if (pendingBatch.Count == 0)
                return;

            var msg = new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkSyncVar
            };
            if (pendingBatch.Count == 1)
                msg.NetworkSyncVar = pendingBatch[0];
            else
                msg.NetworkSyncVars.AddRange(pendingBatch);

            pendingBatch.Clear();
            GameNet.Send(msg);
        }

        BinaryWriter PrepareWriter()
        {
            if (serializeStream == null)
            {
                serializeStream = new MemoryStream(64);
                serializeWriter = new BinaryWriter(serializeStream, Encoding.UTF8, true);
            }
            serializeStream.SetLength(0);
            serializeStream.Position = 0;
            return serializeWriter;
        }

        BinaryReader PrepareReader(ByteString bytes)
        {
            if (deserializeStream == null)
            {
                deserializeStream = new ByteStringInputStream();
                deserializeReader = new BinaryReader(deserializeStream, Encoding.UTF8, true);
            }
            deserializeStream.Reset(bytes);
            return deserializeReader;
        }

        void BuildBindings()
        {
            bindings.Clear();
            bindingList.Clear();
            tempBehaviours.Clear();
            GetComponents(tempBehaviours);
            for (int bIndex = 0; bIndex < tempBehaviours.Count; bIndex++)
            {
                MonoBehaviour component = tempBehaviours[bIndex];
                if (component == null) continue;
                Type type = component.GetType();
                uint behaviourId = StableHash(type.FullName ?? type.Name);
                for (Type current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        SyncVarAttribute attribute = field.GetCustomAttribute<SyncVarAttribute>();
                        if (attribute == null) continue;
                        ulong key = MakeKey(behaviourId, attribute.FieldId);
                        if (bindings.ContainsKey(key))
                        {
                            Debug.LogError($"[SyncVar] 重复字段 ID：{type.FullName} / {attribute.FieldId}", this);
                            continue;
                        }

                        MethodInfo hook = ResolveHook(type, field.FieldType, attribute.Hook);
                        if (!string.IsNullOrWhiteSpace(attribute.Hook) && hook == null)
                            Debug.LogWarning($"[SyncVar] 找不到签名匹配的 Hook：{type.FullName}.{attribute.Hook}", this);

                        MethodInfo getter = current.GetMethod(SyncGetPrefix + field.Name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        MethodInfo setter = current.GetMethod(SyncSetPrefix + field.Name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                        SyncBinding binding = CreateBinding(component, field, getter, setter, hook);
                        if (binding == null)
                        {
                            Debug.LogError($"[SyncVar] 不支持的同步字段类型：{type.FullName}.{field.Name} ({field.FieldType.FullName})", this);
                            continue;
                        }
                        binding.Attribute = attribute;
                        binding.BehaviourId = behaviourId;
                        bindings.Add(key, binding);
                        bindingList.Add(binding);
                    }
                }
            }
            tempBehaviours.Clear();
        }

        static SyncBinding CreateBinding(MonoBehaviour target, FieldInfo field, MethodInfo getter, MethodInfo setter, MethodInfo hook)
        {
            Type t = field.FieldType;
            if (t == typeof(bool)) return new TypedBinding<bool>(target, field, getter, setter, hook, 1, (v, w) => w.Write(v), r => r.ReadBoolean());
            if (t == typeof(byte)) return new TypedBinding<byte>(target, field, getter, setter, hook, 2, (v, w) => w.Write(v), r => r.ReadByte());
            if (t == typeof(sbyte)) return new TypedBinding<sbyte>(target, field, getter, setter, hook, 3, (v, w) => w.Write(v), r => r.ReadSByte());
            if (t == typeof(short)) return new TypedBinding<short>(target, field, getter, setter, hook, 4, (v, w) => w.Write(v), r => r.ReadInt16());
            if (t == typeof(ushort)) return new TypedBinding<ushort>(target, field, getter, setter, hook, 5, (v, w) => w.Write(v), r => r.ReadUInt16());
            if (t == typeof(int)) return new TypedBinding<int>(target, field, getter, setter, hook, 6, (v, w) => w.Write(v), r => r.ReadInt32());
            if (t == typeof(uint)) return new TypedBinding<uint>(target, field, getter, setter, hook, 7, (v, w) => w.Write(v), r => r.ReadUInt32());
            if (t == typeof(long)) return new TypedBinding<long>(target, field, getter, setter, hook, 8, (v, w) => w.Write(v), r => r.ReadInt64());
            if (t == typeof(ulong)) return new TypedBinding<ulong>(target, field, getter, setter, hook, 9, (v, w) => w.Write(v), r => r.ReadUInt64());
            if (t == typeof(float)) return new TypedBinding<float>(target, field, getter, setter, hook, 10, (v, w) => w.Write(v), r => r.ReadSingle());
            if (t == typeof(double)) return new TypedBinding<double>(target, field, getter, setter, hook, 11, (v, w) => w.Write(v), r => r.ReadDouble());
            if (t == typeof(string)) return new TypedBinding<string>(target, field, getter, setter, hook, 12, (v, w) => w.Write(v ?? string.Empty), r => r.ReadString());
            if (t.IsEnum) return new EnumBinding(target, field, getter, setter, hook);
            if (t == typeof(Vector2))
                return new TypedBinding<Vector2>(target, field, getter, setter, hook, 14,
                    (v, w) => { w.Write(v.x); w.Write(v.y); },
                    r => new Vector2(r.ReadSingle(), r.ReadSingle()));
            if (t == typeof(Vector3))
                return new TypedBinding<Vector3>(target, field, getter, setter, hook, 15,
                    (v, w) => { w.Write(v.x); w.Write(v.y); w.Write(v.z); },
                    r => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
            if (t == typeof(Quaternion))
                return new TypedBinding<Quaternion>(target, field, getter, setter, hook, 16,
                    (v, w) => { w.Write(v.x); w.Write(v.y); w.Write(v.z); w.Write(v.w); },
                    r => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
            if (t == typeof(byte[]))
                return new TypedBinding<byte[]>(target, field, getter, setter, hook, 17,
                    null,
                    r => r.ReadBytes((int)r.BaseStream.Length),
                    ByteArrayEquals,
                    v => v != null ? (byte[])v.Clone() : null);
            return null;
        }

        internal void OnSyncVarReceived(NetworkSyncVarData data)
        {
            if (HasAuthority || data.ObjectId != NetId ||
                !bindings.TryGetValue(MakeKey(data.BehaviourId, data.FieldId), out SyncBinding binding) ||
                data.Sequence <= binding.ReceiveSequence ||
                binding.Target == null)
                return;

            binding.ApplyReceived(this, data.Value, data.ValueType, data.Sequence);
        }

        internal void ResetNetworkState()
        {
            for (int i = 0; i < bindingList.Count; i++)
                bindingList[i].Reset();
            nextSyncTime = 0f;
        }

        void OnAuthorityChanged(NetworkIdentity changed)
        {
            if (changed != Identity) return;
            ResetNetworkState();
        }

        private static MethodInfo ResolveHook(Type type, Type fieldType, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
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

        void OnDestroy()
        {
            serializeWriter?.Dispose();
            serializeStream?.Dispose();
            deserializeReader?.Dispose();
            deserializeStream?.Dispose();
            bindings.Clear();
            bindingList.Clear();
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

        static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

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
