using System;
using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketDemo.Transport
{
    /// <summary>
    /// TCP 与 KCP 共享的帧类型及 UDP 信令包定义（客户端与服务端必须保持一致）。
    /// </summary>
    public static class TransportProtocol
    {
        public const int MaxFrameBytes = 1024 * 1024;
        public const int BindTokenBytes = 16;

        // TCP/KCP 业务消息帧类型（紧跟在长度或 KCP 消息首字节之后）
        public const byte FrameBinary = 0x01;
        public const byte FrameText = 0x02;
        public const byte FramePing = 0x03;
        public const byte FramePong = 0x04;
        public const byte FrameClose = 0x05;
        /// <summary>服务端在 TCP 建连后下发：4B conv + 16B token，客户端可据此附着 KCP 实时通道。</summary>
        public const byte FrameBindInfo = 0x06;

        // KCP UDP 报文外层首字节（区分握手/绑定/关闭与 KCP 段数据）
        public const byte UdpConnectReq = 0x01; // [1B kind][4B nonce]
        public const byte UdpConnectAck = 0x02; // [1B kind][4B nonce][4B conv]
        public const byte UdpBindReq = 0x03;    // [1B kind][4B conv][16B token]
        public const byte UdpBindAck = 0x04;    // [1B kind][4B conv]
        public const byte UdpBindReject = 0x05; // [1B kind][4B conv]
        public const byte UdpClose = 0x06;      // [1B kind][4B conv][2B closeCode]
        public const byte UdpKcpData = 0x10;    // [1B kind][KCP raw segment...]
        public const byte UdpUnreliableData = 0x11; // [1B kind][4B conv][4B authTag][4B seq][1B frameType][payload...]
        public const int UnreliableHeaderBytes = 14;
        public const int SafeUnreliablePayloadBytes = 1160;

        public static byte[] BuildBindInfoFrame(uint conv, ReadOnlySpan<byte> token)
        {
            byte[] payload = new byte[4 + BindTokenBytes];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, conv);
            token.Slice(0, BindTokenBytes).CopyTo(payload.AsSpan(4));
            return payload;
        }

        public static byte[] BuildClosePayload(WebSocketCloseStatus status, string? reason)
        {
            byte[] reasonBytes = string.IsNullOrEmpty(reason) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(reason);
            byte[] payload = new byte[2 + reasonBytes.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)status);
            if (reasonBytes.Length > 0)
                Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            return payload;
        }

        public static (WebSocketCloseStatus Status, string Reason) ParseClosePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 2)
                return (WebSocketCloseStatus.NormalClosure, string.Empty);
            var status = (WebSocketCloseStatus)BinaryPrimitives.ReadUInt16LittleEndian(payload);
            string reason = payload.Length > 2 ? Encoding.UTF8.GetString(payload.Slice(2)) : string.Empty;
            return (status, reason);
        }
    }
}
