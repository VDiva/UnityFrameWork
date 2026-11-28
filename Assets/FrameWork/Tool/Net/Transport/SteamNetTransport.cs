#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Text;
using UnityWebSocket;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// Steam 底层网络发送/接收适配接口（后续接入 Steamworks.NET 或 Facepunch.Steamworks 时只需实现此接口）。
    /// - SendReliable 对应 SteamNetworkingSockets 的 ReliableNoNagle；
    /// - SendUnreliable 对应 UnreliableNoDelay（承载高频位置与语音帧，无队头阻塞）。
    /// </summary>
    public interface ISteamTransportBackend : IDisposable
    {
        bool IsConnected { get; }
        event Action OnConnected;
        event Action<ushort, string> OnDisconnected;
        event Action<string, Exception> OnError;
        event Action<byte[], int> OnBinaryReceived;

        void Connect(string targetAddress);
        void Close();
        void SendReliable(byte[] data, int length);
        void SendUnreliable(byte[] data, int length);
        void PollEvents();
    }

    /// <summary>
    /// 预留的 Steam 网络传输层（steam://&lt;HostSteamId&gt;）。
    /// 完整复用框架的延迟编码（IDeferredNetPayload）、Latest 覆写合并与主线程泵，
    /// 接入真实 Steam SDK 时只需通过 <see cref="RegisterBackendFactory"/> 注入实现即可。
    /// </summary>
    public sealed class SteamNetTransport : INetTransport, IMainThreadPumpable
    {
        private static Func<ISteamTransportBackend> _backendFactory;

        private readonly Dictionary<ulong, PendingLatestItem> _pendingLatest = new Dictionary<ulong, PendingLatestItem>();
        private readonly List<ulong> _flushKeysScratch = new List<ulong>(32);

        private ISteamTransportBackend _backend;
        private volatile WebSocketState _state = WebSocketState.Closed;
        private int _bufferedSendBytes;
        private bool _closedNotified;

        private struct PendingLatestItem
        {
            public byte[] Data;
            public IDeferredNetPayload Deferred;
            public int TrackedBytes;
            public int QueuedAt;
            public int MaxAgeMs;
        }

        public string Address { get; }
        public string TransportName => "Steam";
        public bool IsRealtimeChannelActive => _state == WebSocketState.Open;
        public WebSocketState ReadyState => _state;
        public int PendingSendCount => _pendingLatest.Count;
        public int BufferedSendBytes => _bufferedSendBytes;

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<MessageEventArgs> OnMessage;

        public SteamNetTransport(string address)
        {
            Address = address;
        }

        /// <summary>注入 Steam SDK 后端工厂（例如 Steamworks.NET / Facepunch.Steamworks 适配器）。</summary>
        public static void RegisterBackendFactory(Func<ISteamTransportBackend> factory)
        {
            _backendFactory = factory;
        }

        public static bool HasBackend => _backendFactory != null;

        public void ConnectAsync()
        {
            if (_backendFactory == null)
            {
                _state = WebSocketState.Closed;
                OnError?.Invoke(this, new ErrorEventArgs(
                    "尚未注册 Steam 网络后端。请先调用 SteamNetTransport.RegisterBackendFactory(...) 注入 Steamworks 适配器。", null));
                NotifyCloseOnce((ushort)CloseStatusCode.Abnormal, "Steam backend not registered");
                return;
            }

            _closedNotified = false;
            _state = WebSocketState.Connecting;
            _backend = _backendFactory();
            _backend.OnConnected += HandleBackendConnected;
            _backend.OnDisconnected += HandleBackendDisconnected;
            _backend.OnError += HandleBackendError;
            _backend.OnBinaryReceived += HandleBackendBinaryReceived;

            NetTransportPump.Register(this);
            _backend.Connect(Address);
        }

        public void CloseAsync()
        {
            if (_state == WebSocketState.Closed) return;
            _state = WebSocketState.Closing;
            ClearPendingLatest();
            try { _backend?.Close(); } catch { }
            CleanupBackend();
            _state = WebSocketState.Closed;
            NotifyCloseOnce((ushort)CloseStatusCode.Normal, "Normal Closure");
        }

        public void SendAsync(byte[] data)
        {
            if (_state != WebSocketState.Open || data == null || _backend == null) return;
            FlushLatest();
            _backend.SendReliable(data, data.Length);
        }

        public void SendAsync(string text)
        {
            if (_state != WebSocketState.Open || text == null || _backend == null) return;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            SendAsync(bytes);
        }

        public void SendRealtimeAsync(byte[] data)
        {
            if (_state != WebSocketState.Open || data == null || _backend == null) return;
            _backend.SendUnreliable(data, data.Length);
        }

        public void SendLatestAsync(byte[] data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            if (_state != WebSocketState.Open || data == null) return;
            if (_pendingLatest.TryGetValue(key, out PendingLatestItem existing))
            {
                byte[] prev = existing.Data ?? existing.Deferred?.Encode();
                byte[] merged = merge == null || prev == null ? data : merge(prev, data);
                int len = merged != null ? merged.Length : 0;
                _bufferedSendBytes += len - existing.TrackedBytes;
                _pendingLatest[key] = new PendingLatestItem
                {
                    Data = merged,
                    Deferred = null,
                    TrackedBytes = len,
                    QueuedAt = Environment.TickCount,
                    MaxAgeMs = maxAgeMs
                };
            }
            else
            {
                _bufferedSendBytes += data.Length;
                _pendingLatest[key] = new PendingLatestItem
                {
                    Data = data,
                    Deferred = null,
                    TrackedBytes = data.Length,
                    QueuedAt = Environment.TickCount,
                    MaxAgeMs = maxAgeMs
                };
            }
        }

        public void SendLatestDeferredAsync(IDeferredNetPayload payload, ulong key, int maxAgeMs = 0)
        {
            if (_state != WebSocketState.Open || payload == null) return;
            if (_pendingLatest.TryGetValue(key, out PendingLatestItem existing))
            {
                IDeferredNetPayload merged = existing.Deferred != null ? payload.MergeFrom(existing.Deferred) : payload;
                int est = Math.Max(0, merged.EstimatedBytes);
                _bufferedSendBytes += est - existing.TrackedBytes;
                _pendingLatest[key] = new PendingLatestItem
                {
                    Data = null,
                    Deferred = merged,
                    TrackedBytes = est,
                    QueuedAt = Environment.TickCount,
                    MaxAgeMs = maxAgeMs
                };
            }
            else
            {
                int est = Math.Max(0, payload.EstimatedBytes);
                _bufferedSendBytes += est;
                _pendingLatest[key] = new PendingLatestItem
                {
                    Data = null,
                    Deferred = payload,
                    TrackedBytes = est,
                    QueuedAt = Environment.TickCount,
                    MaxAgeMs = maxAgeMs
                };
            }
        }

        public void FlushLatest()
        {
            if (_state != WebSocketState.Open || _backend == null || _pendingLatest.Count == 0)
                return;

            _flushKeysScratch.Clear();
            foreach (ulong k in _pendingLatest.Keys)
                _flushKeysScratch.Add(k);

            int now = Environment.TickCount;
            for (int i = 0; i < _flushKeysScratch.Count; i++)
            {
                ulong key = _flushKeysScratch[i];
                if (!_pendingLatest.TryGetValue(key, out PendingLatestItem item))
                    continue;
                _pendingLatest.Remove(key);
                _bufferedSendBytes = Math.Max(0, _bufferedSendBytes - item.TrackedBytes);

                if (item.MaxAgeMs > 0 && unchecked(now - item.QueuedAt) > item.MaxAgeMs)
                    continue;

                byte[] payload = item.Data ?? item.Deferred?.Encode();
                if (payload == null || payload.Length == 0)
                    continue;

                // 有有效期（位置快照等）走非可靠通道，无有效期（动画状态等）走可靠通道。
                if (item.MaxAgeMs > 0)
                    _backend.SendUnreliable(payload, payload.Length);
                else
                    _backend.SendReliable(payload, payload.Length);
            }
            _flushKeysScratch.Clear();
        }

        void IMainThreadPumpable.PumpMainThreadEvents()
        {
            _backend?.PollEvents();
        }

        void IMainThreadPumpable.AbortOnQuit()
        {
            CloseAsync();
        }

        private void HandleBackendConnected()
        {
            _state = WebSocketState.Open;
            OnOpen?.Invoke(this, new OpenEventArgs());
        }

        private void HandleBackendDisconnected(ushort code, string reason)
        {
            ClearPendingLatest();
            CleanupBackend();
            _state = WebSocketState.Closed;
            NotifyCloseOnce(code, reason ?? string.Empty);
        }

        private void HandleBackendError(string message, Exception ex)
        {
            OnError?.Invoke(this, new ErrorEventArgs(message, ex));
        }

        private void HandleBackendBinaryReceived(byte[] data, int length)
        {
            if (data == null || length <= 0)
            {
                OnMessage?.Invoke(this, MessageEventArgs.CreateBinary(Array.Empty<byte>()));
                return;
            }
            byte[] copy = new byte[length];
            Buffer.BlockCopy(data, 0, copy, 0, length);
            OnMessage?.Invoke(this, MessageEventArgs.CreateBinary(copy));
        }

        private void ClearPendingLatest()
        {
            _pendingLatest.Clear();
            _bufferedSendBytes = 0;
        }

        private void CleanupBackend()
        {
            NetTransportPump.Unregister(this);
            ISteamTransportBackend b = _backend;
            _backend = null;
            if (b == null) return;
            b.OnConnected -= HandleBackendConnected;
            b.OnDisconnected -= HandleBackendDisconnected;
            b.OnError -= HandleBackendError;
            b.OnBinaryReceived -= HandleBackendBinaryReceived;
            try { b.Dispose(); } catch { }
        }

        private void NotifyCloseOnce(ushort code, string reason)
        {
            if (_closedNotified) return;
            _closedNotified = true;
            OnClose?.Invoke(this, new CloseEventArgs(code, reason));
        }
    }
}
#endif
