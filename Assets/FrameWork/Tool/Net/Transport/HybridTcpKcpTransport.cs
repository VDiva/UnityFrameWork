#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using UnityEngine;
using UnityWebSocket;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// TCP + KCP 双通道混合传输层：
    /// - TCP 作为主通道承载登录、RPC、聊天、背包等可靠有序消息；
    /// - TCP 建连后收到服务端下发的 (conv, token)，异步附着 KCP 作为低延迟实时通道承载 SendLatest（位置/动画等）；
    /// - 若 UDP 被防火墙拦截或 KCP 意外断开，实时消息自动无缝回退到 TCP，不中断会话。
    /// </summary>
    public sealed class HybridTcpKcpTransport : INetTransport
    {
        private readonly TcpTransport _tcp;
        private readonly string _kcpAddress;
        private KcpTransport _kcp;
        private bool _closing;

        public string Address => _tcp.Address;
        public string KcpAddress => _kcpAddress;
        public string TransportName => IsRealtimeChannelActive ? "TCP+KCP" : "TCP";
        public bool IsRealtimeChannelActive => _kcp != null && _kcp.ReadyState == WebSocketState.Open;
        public WebSocketState ReadyState => _tcp.ReadyState;
        public int PendingSendCount => _tcp.PendingSendCount + (_kcp?.PendingSendCount ?? 0);
        public int BufferedSendBytes => _tcp.BufferedSendBytes + (_kcp?.BufferedSendBytes ?? 0);

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<MessageEventArgs> OnMessage;

        public HybridTcpKcpTransport(string tcpAddress, string kcpAddress)
        {
            _tcp = new TcpTransport(tcpAddress);
            _kcpAddress = kcpAddress;

            _tcp.OnOpen += HandleTcpOpen;
            _tcp.OnClose += HandleTcpClose;
            _tcp.OnError += HandleTcpError;
            _tcp.OnMessage += HandleTcpMessage;
            _tcp.OnBindInfoReceived += HandleBindInfoReceived;
        }

        public void ConnectAsync()
        {
            _closing = false;
            _tcp.ConnectAsync();
        }

        public void CloseAsync()
        {
            _closing = true;
            DetachAndCloseKcp();
            _tcp.CloseAsync();
        }

        public void SendAsync(byte[] data) => _tcp.SendAsync(data);
        public void SendAsync(string text) => _tcp.SendAsync(text);

        public void SendRealtimeAsync(byte[] data)
        {
            if (IsRealtimeChannelActive)
                _kcp.SendRealtimeAsync(data);
            else
                _tcp.SendAsync(data);
        }

        public void SendLatestAsync(byte[] data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            if (IsRealtimeChannelActive)
                _kcp.SendLatestAsync(data, key, merge, maxAgeMs);
            else
                _tcp.SendLatestAsync(data, key, merge, maxAgeMs);
        }

        public void SendLatestDeferredAsync(IDeferredNetPayload payload, ulong key, int maxAgeMs = 0)
        {
            if (IsRealtimeChannelActive)
                _kcp.SendLatestDeferredAsync(payload, key, maxAgeMs);
            else
                _tcp.SendLatestDeferredAsync(payload, key, maxAgeMs);
        }

        public void FlushLatest()
        {
            _kcp?.FlushLatest();
            _tcp.FlushLatest();
        }

        private void HandleTcpOpen(object sender, OpenEventArgs e)
        {
            OnOpen?.Invoke(this, e);
        }

        private void HandleTcpMessage(object sender, MessageEventArgs e)
        {
            OnMessage?.Invoke(this, e);
        }

        private void HandleTcpError(object sender, ErrorEventArgs e)
        {
            OnError?.Invoke(this, e);
        }

        private void HandleTcpClose(object sender, CloseEventArgs e)
        {
            _closing = true;
            DetachAndCloseKcp();
            OnClose?.Invoke(this, e);
        }

        private void HandleBindInfoReceived(uint conv, byte[] bindToken)
        {
            if (_closing || string.IsNullOrWhiteSpace(_kcpAddress))
                return;

            DetachAndCloseKcp();

            var kcp = KcpTransport.CreateBound(_kcpAddress, conv, bindToken);
            _kcp = kcp;
            kcp.OnOpen += HandleKcpOpen;
            kcp.OnMessage += HandleKcpMessage;
            kcp.OnError += HandleKcpError;
            kcp.OnClose += HandleKcpClose;
            kcp.ConnectAsync();
        }

        private void HandleKcpOpen(object sender, OpenEventArgs e)
        {
            if (!ReferenceEquals(sender, _kcp)) return;
            Debug.Log($"[HybridTransport] KCP 实时通道已附着 ({_kcpAddress}, conv={_kcp.Conv})");
        }

        private void HandleKcpMessage(object sender, MessageEventArgs e)
        {
            if (!ReferenceEquals(sender, _kcp)) return;
            OnMessage?.Invoke(this, e);
        }

        private void HandleKcpError(object sender, ErrorEventArgs e)
        {
            if (!ReferenceEquals(sender, _kcp)) return;
            Debug.LogWarning($"[HybridTransport] KCP 实时通道异常，已回退到 TCP: {e.Message}");
        }

        private void HandleKcpClose(object sender, CloseEventArgs e)
        {
            if (!ReferenceEquals(sender, _kcp)) return;
            if (!_closing)
                Debug.LogWarning($"[HybridTransport] KCP 实时通道关闭（code={e.Code}），后续实时消息自动回退到 TCP。");
            DetachKcpEvents(_kcp);
            _kcp = null;
        }

        private void DetachAndCloseKcp()
        {
            KcpTransport old = _kcp;
            _kcp = null;
            if (old == null) return;
            DetachKcpEvents(old);
            try { old.CloseAsync(); }
            catch { }
        }

        private void DetachKcpEvents(KcpTransport kcp)
        {
            kcp.OnOpen -= HandleKcpOpen;
            kcp.OnMessage -= HandleKcpMessage;
            kcp.OnError -= HandleKcpError;
            kcp.OnClose -= HandleKcpClose;
        }
    }
}
#endif
