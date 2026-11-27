using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WebSocketDemo.Transport
{
    /// <summary>
    /// 管理 TCP 主会话对应的 KCP 实时通道绑定令牌（单次分配唯一 conv + 16 字节随机令牌）。
    /// </summary>
    public static class KcpBindRegistry
    {
        private sealed record BindEntry(byte[] Token, PlayerSession Session);

        private static int _nextConv = 1000;
        private static readonly ConcurrentDictionary<uint, BindEntry> Entries = new();

        public static uint AllocateConv()
        {
            while (true)
            {
                uint candidate = unchecked((uint)Interlocked.Increment(ref _nextConv));
                if (candidate != 0 && !Entries.ContainsKey(candidate))
                    return candidate;
            }
        }

        public static (uint Conv, byte[] Token) RegisterSession(PlayerSession session)
        {
            byte[] token = RandomNumberGenerator.GetBytes(TransportProtocol.BindTokenBytes);
            while (true)
            {
                uint conv = AllocateConv();
                if (Entries.TryAdd(conv, new BindEntry(token, session)))
                    return (conv, token);
            }
        }

        public static bool TryValidateAndGetSession(uint conv, ReadOnlySpan<byte> token, out PlayerSession? session)
        {
            session = null;
            if (token.Length != TransportProtocol.BindTokenBytes) return false;
            if (!Entries.TryGetValue(conv, out var entry)) return false;
            if (!CryptographicOperations.FixedTimeEquals(entry.Token, token)) return false;
            if (!entry.Session.IsConnected) return false;
            session = entry.Session;
            return true;
        }

        public static void Unregister(uint conv)
        {
            if (conv != 0) Entries.TryRemove(conv, out _);
        }
    }
}
