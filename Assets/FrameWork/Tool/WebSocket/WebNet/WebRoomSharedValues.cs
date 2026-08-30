using System;
using System.Collections.Generic;
using GameData;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>房间共享整数的客户端只读镜像。float 以保留两位小数的定点 long 传输。</summary>
    public static class WebRoomSharedValues
    {
        const int MaxKeyLength = 64;
        const long MaxDelta = 1_000_000_000;
        const int FloatScale = 100;
        static readonly Dictionary<string, Entry> values = new Dictionary<string, Entry>();

        public static event Action<string, long, long> ValueChanged;
        public static event Action ResetCompleted;

        struct Entry
        {
            public long Value;
            public ulong Version;
        }

        
        public static bool Add(string key, long delta)
        {
            key = NormalizeKey(key);
            if (!LobbyWebNet.IsConnected || key == null || delta == 0 || delta < -MaxDelta || delta > MaxDelta)
                return false;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.AddRoomSharedValue,
                RoomSharedValue = new RoomSharedValueData { Key = key, Delta = delta }
            });
            return true;
        }
        
        public static bool Add(string key, float delta)
        {
            if (!TryToScaledLong(delta, out long scaledDelta) || scaledDelta == 0)
                return false;
            return Add(key, scaledDelta);
        }

        public static bool Set(string key, long value)
        {
            key = NormalizeKey(key);
            if (!LobbyWebNet.IsConnected || key == null || value < -MaxDelta || value > MaxDelta)
                return false;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.SetRoomSharedValue,
                RoomSharedValue = new RoomSharedValueData { Key = key, Value = value }
            });
            return true;
        }

        public static bool Set(string key, float value)
        {
            if (!TryToScaledLong(value, out long scaledValue))
                return false;
            return Set(key, scaledValue);
        }

        public static bool ResetAll()
        {
            // 调用者明确要求重置时，本地镜像立即清除，不能等待网络往返；
            // 服务端确认后仍会广播 RoomSharedValuesReset 给房间内其他成员。
            Clear();
            if (!LobbyWebNet.IsConnected) return false;
            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.ResetRoomSharedValues
            });
            return true;
        }

        
        public static long Get(string key, long defaultValue = 0)
        {
            key = NormalizeKey(key);
            return key != null && values.TryGetValue(key, out Entry entry) ? entry.Value : defaultValue;
        }
        
        public static bool TryGet(string key, out long value)
        {
            key = NormalizeKey(key);
            if (key != null && values.TryGetValue(key, out Entry entry))
            {
                value = entry.Value;
                return true;
            }
            value = default;
            return false;
        }
        public static float GetFloat(string key, float defaultValue = 0f)
        {
            key = NormalizeKey(key);
            return key != null && values.TryGetValue(key, out Entry entry)
                ? ToFloat(entry.Value)
                : defaultValue;
        }

        /// <summary>将网络传输的定点 long 还原为保留两位小数的 float。</summary>
        public static float ToFloat(long value)
        {
            return value / (float)FloatScale;
        }
        
        public static bool TryGet(string key, out float value)
        {
            key = NormalizeKey(key);
            if (key != null && values.TryGetValue(key, out Entry entry))
            {
                value = ToFloat(entry.Value);
                return true;
            }
            value = default;
            return false;
        }

        internal static void Apply(RoomSharedValueData data)
        {
            string key = NormalizeKey(data?.Key);
            if (key == null) return;
            if (values.TryGetValue(key, out Entry old) && data.Version <= old.Version) return;

            long oldValue = old.Value;
            values[key] = new Entry { Value = data.Value, Version = data.Version };
            ValueChanged?.Invoke(key, oldValue, data.Value);
        }

        internal static void Clear()
        {
            values.Clear();
        }

        internal static void NotifyResetCompleted()
        {
            ResetCompleted?.Invoke();
        }

        static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            string normalized = key.Trim();
            return normalized.Length <= MaxKeyLength ? normalized : null;
        }

        static bool TryToScaledLong(float value, out long scaledValue)
        {
            scaledValue = 0;
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;

            double rounded = Math.Round((double)value * FloatScale, MidpointRounding.AwayFromZero);
            if (rounded < -MaxDelta || rounded > MaxDelta) return false;

            scaledValue = (long)rounded;
            return true;
        }

    }
}
