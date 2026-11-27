using System;
using UnityWebSocket;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 基于 UnityWebSocket 的传输层适配实现（WebGL / 微信小游戏强制使用，其它平台亦可选用）。
    /// </summary>
    public sealed class WebSocketTransport : INetTransport
    {
        private readonly UnityWebSocket.WebSocket _socket;

        public string Address => _socket.Address;
        public string TransportName => "WebSocket";
        public bool IsRealtimeChannelActive => false;
        public WebSocketState ReadyState => _socket.ReadyState;
        public int PendingSendCount => _socket.PendingSendCount;
        public int BufferedSendBytes => _socket.BufferedSendBytes;

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<MessageEventArgs> OnMessage;

        public WebSocketTransport(string address)
        {
            _socket = new UnityWebSocket.WebSocket(address);
            _socket.OnOpen += HandleOpen;
            _socket.OnClose += HandleClose;
            _socket.OnError += HandleError;
            _socket.OnMessage += HandleMessage;
        }

        public void ConnectAsync() => _socket.ConnectAsync();
        public void CloseAsync() => _socket.CloseAsync();
        public void SendAsync(byte[] data) => _socket.SendAsync(data);
        public void SendAsync(string text) => _socket.SendAsync(text);
        public void SendRealtimeAsync(byte[] data) => _socket.SendAsync(data);

        public void SendLatestAsync(byte[] data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            _socket.SendLatestAsync(data, key, merge, maxAgeMs);
        }

        public void SendLatestDeferredAsync(IDeferredNetPayload payload, ulong key, int maxAgeMs = 0)
        {
            if (payload == null) return;
            _socket.SendLatestAsync(payload.Encode(), key, null, maxAgeMs);
        }

        public void FlushLatest() => _socket.FlushLatest();

        private void HandleOpen(object sender, OpenEventArgs e) => OnOpen?.Invoke(this, e);
        private void HandleClose(object sender, CloseEventArgs e) => OnClose?.Invoke(this, e);
        private void HandleError(object sender, ErrorEventArgs e) => OnError?.Invoke(this, e);
        private void HandleMessage(object sender, MessageEventArgs e) => OnMessage?.Invoke(this, e);
    }
}
