using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketDemo.Transport
{
    public readonly struct TransportReceiveResult
    {
        public readonly WebSocketMessageType MessageType;
        public readonly byte[]? Data;
        public readonly int Length;
        public readonly WebSocketCloseStatus? CloseStatus;
        public readonly string? CloseDescription;

        public TransportReceiveResult(WebSocketMessageType messageType, byte[]? data, int length,
            WebSocketCloseStatus? closeStatus = null, string? closeDescription = null)
        {
            MessageType = messageType;
            Data = data;
            Length = length;
            CloseStatus = closeStatus;
            CloseDescription = closeDescription;
        }

        public static TransportReceiveResult Closed(
            WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
            string? description = null) =>
            new(WebSocketMessageType.Close, null, 0, status, description);
    }

    /// <summary>
    /// 服务端统一连接传输抽象，屏蔽 WebSocket / TCP / KCP 底层差异。
    /// </summary>
    public interface IClientTransport : IDisposable
    {
        string TransportName { get; }
        bool IsConnected { get; }
        Task<TransportReceiveResult> ReceiveMessageAsync(CancellationToken token);
        Task SendAsync(byte[] data, WebSocketMessageType type, CancellationToken token);
        ValueTask SendUnreliableAsync(byte[] data, WebSocketMessageType type, CancellationToken token) =>
            new(SendAsync(data, type, token));
        Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken token);
        void Abort();
    }
}
