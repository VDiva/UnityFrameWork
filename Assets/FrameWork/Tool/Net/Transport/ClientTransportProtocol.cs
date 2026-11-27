#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Text;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 与服务端 <c>WebSocketDemo.Transport.TransportProtocol</c> 完全一致的帧格式与 UDP 信令常量。
    /// </summary>
    internal static class ClientTransportProtocol
    {
        public const int MaxFrameBytes = 1024 * 1024;
        public const int BindTokenBytes = 16;

        public const byte FrameBinary = 0x01;
        public const byte FrameText = 0x02;
        public const byte FramePing = 0x03;
        public const byte FramePong = 0x04;
        public const byte FrameClose = 0x05;
        public const byte FrameBindInfo = 0x06;

        public const byte UdpConnectReq = 0x01;
        public const byte UdpConnectAck = 0x02;
        public const byte UdpBindReq = 0x03;
        public const byte UdpBindAck = 0x04;
        public const byte UdpBindReject = 0x05;
        public const byte UdpClose = 0x06;
        public const byte UdpKcpData = 0x10;
        public const byte UdpUnreliableData = 0x11;
        public const int UnreliableHeaderBytes = 14;
        public const int SafeUnreliablePayloadBytes = 1160;

        public static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        public static void WriteUInt32LE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public static void WriteInt32LE(byte[] buffer, int offset, int value)
        {
            WriteUInt32LE(buffer, offset, unchecked((uint)value));
        }

        public static ushort ReadUInt16LE(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        public static uint ReadUInt32LE(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));
        }

        public static int ReadInt32LE(byte[] buffer, int offset)
        {
            return unchecked((int)ReadUInt32LE(buffer, offset));
        }

        public static byte[] BuildClosePayload(ushort code, string reason)
        {
            byte[] reasonBytes = string.IsNullOrEmpty(reason) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(reason);
            byte[] payload = new byte[2 + reasonBytes.Length];
            WriteUInt16LE(payload, 0, code);
            if (reasonBytes.Length > 0)
                Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            return payload;
        }

        public static void ParseClosePayload(byte[] payload, int offset, int count, out ushort code, out string reason)
        {
            if (payload == null || count < 2)
            {
                code = 1000;
                reason = string.Empty;
                return;
            }
            code = ReadUInt16LE(payload, offset);
            reason = count > 2 ? Encoding.UTF8.GetString(payload, offset + 2, count - 2) : string.Empty;
        }

        public static bool TryParseEndpoint(string address, int defaultPort, out string host, out int port)
        {
            host = null;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            string trimmed = address.Trim();
            if (!trimmed.Contains("://"))
                trimmed = "tcp://" + trimmed;

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri) || string.IsNullOrWhiteSpace(uri.Host))
                return false;

            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : defaultPort;
            return true;
        }
    }
}
#endif
