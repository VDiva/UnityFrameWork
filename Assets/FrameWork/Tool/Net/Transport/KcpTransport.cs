#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityWebSocket;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 原生平台 KCP（UDP）传输层：
    /// 1) 独立模式（kcp://host:port）：发起 UdpConnectReq 握手，作为主连接承载全部消息；
    /// 2) 附着模式（CreateBound）：携带 TCP 下发的 conv+token 发起 UdpBindReq，作为实时通道承载高频同步帧与语音帧。
    /// 性能优化：Check() 精确自适应调度 + 事件信号零延迟唤醒 + 收发缓冲复用 + 2MB UDP 套接字缓冲。
    /// </summary>
    public sealed class KcpTransport : INetTransport, IMainThreadPumpable
    {
        private const int DefaultKcpPort = 5102;
        private const int MaxKcpMessageBytes = 250 * 1024;
        private const int UdpSocketBufferBytes = 2 * 1024 * 1024;
        private const int MaxReusableBufBytes = 64 * 1024;
        private const int MaxPooledItems = 256;
        private const int HandshakeRetryMs = 250;
        private const int StandaloneHandshakeTimeoutMs = 5000;
        private const int BindHandshakeTimeoutMs = 3000;
        private const int PingIntervalMs = 5000;
        private const int IdleTimeoutMs = 25000;

        private sealed class SendItem
        {
            public byte FrameType;
            public byte[] Data;
            public IDeferredNetPayload Deferred;
            public int TrackedBytes;
            public ulong? Key;
            public int QueuedAt;
            public int MaxAgeMs;
        }

        private struct DequeuedFrame
        {
            public byte FrameType;
            public byte[] Data;
            public IDeferredNetPayload Deferred;
            public int QueuedAt;
            public int MaxAgeMs;
        }

        private readonly bool _isBoundMode;
        private readonly uint _boundConv;
        private readonly byte[] _bindToken;

        private readonly object _kcpLock = new object();
        private readonly object _sendGate = new object();
        private readonly ConcurrentQueue<SendItem> _sendQueue = new ConcurrentQueue<SendItem>();
        private readonly Dictionary<ulong, SendItem> _pendingLatest = new Dictionary<ulong, SendItem>();
        private readonly Stack<SendItem> _itemPool = new Stack<SendItem>();
        private readonly SemaphoreSlim _wakeSignal = new SemaphoreSlim(0, 1);
        private readonly ConcurrentQueue<EventArgs> _eventQueue = new ConcurrentQueue<EventArgs>();

        // 仅在持有 _kcpLock 时读写，避免高频收发反复分配 byte[]。
        private byte[] _sendScratch = new byte[4096];
        private byte[] _recvScratch = new byte[4096];
        private byte[] _udpOutBuf = new byte[2048];

        private UdpClient _udp;
        private IPEndPoint _remoteEndPoint;
        private ClientKcpCore _kcp;
        private CancellationTokenSource _cts;
        private volatile WebSocketState _state = WebSocketState.Closed;
        private int _bufferedSendBytes;
        private int _started;
        private int _closeRequested;
        private int _closeEnqueued;
        private long _lastReceiveTick;
        private uint _conv;
        private uint _authTag;
        private uint _unreliableOutSeq;
        private uint _unreliableInMaxSeq;
        private bool _hasUnreliableInSeq;
        private readonly byte[] _unreliableSendBuf = new byte[ClientTransportProtocol.UnreliableHeaderBytes + ClientTransportProtocol.SafeUnreliablePayloadBytes];
        private readonly object _unreliableSendLock = new object();

        public string Address { get; }
        public uint Conv => _conv;
        public string TransportName => "KCP";
        public bool IsRealtimeChannelActive => _state == WebSocketState.Open;
        public WebSocketState ReadyState => _state;
        public int PendingSendCount => _sendQueue.Count;
        public int BufferedSendBytes => Volatile.Read(ref _bufferedSendBytes);

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<MessageEventArgs> OnMessage;

        public KcpTransport(string address)
        {
            Address = address;
            _isBoundMode = false;
        }

        private KcpTransport(string address, uint conv, byte[] bindToken)
        {
            Address = address;
            _isBoundMode = true;
            _boundConv = conv;
            _bindToken = bindToken;
            if (bindToken != null && bindToken.Length >= 4)
                _authTag = ClientTransportProtocol.ReadUInt32LE(bindToken, 0);
        }

        internal static KcpTransport CreateBound(string kcpAddress, uint conv, byte[] bindToken)
        {
            return new KcpTransport(kcpAddress, conv, bindToken);
        }

        public void ConnectAsync()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                EnqueueError("KCP transport is already active.", null);
                return;
            }

            if (!ClientTransportProtocol.TryParseEndpoint(Address, DefaultKcpPort, out string host, out int port))
            {
                _state = WebSocketState.Closed;
                EnqueueError($"无效的 KCP 地址: {Address}", null);
                EnqueueClose((ushort)CloseStatusCode.Abnormal, "Invalid KCP address");
                return;
            }

            _state = WebSocketState.Connecting;
            _cts = new CancellationTokenSource();
            NetTransportPump.Register(this);
            Task.Run(() => RunAsync(host, port, _cts.Token));
        }

        public void CloseAsync()
        {
            if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
                return;

            if (_state == WebSocketState.Open)
                _state = WebSocketState.Closing;

            SendCloseNotification((ushort)CloseStatusCode.Normal, "Normal Closure");
            AbortInternal();
        }

        public void SendAsync(byte[] data)
        {
            if (_state != WebSocketState.Open || data == null) return;
            EnqueueReliable(ClientTransportProtocol.FrameBinary, data);
        }

        public void SendAsync(string text)
        {
            if (_state != WebSocketState.Open || text == null) return;
            EnqueueReliable(ClientTransportProtocol.FrameText, Encoding.UTF8.GetBytes(text));
        }

        public void SendRealtimeAsync(byte[] data)
        {
            if (_state != WebSocketState.Open || data == null) return;
            // 低延迟非覆写实时流（如 Opus 语音帧）：单包 <= 安全 MTU 时直接走非可靠 UDP 数据报，彻底消除 KCP 队头阻塞。
            if (TrySendUnreliableDirect(ClientTransportProtocol.FrameBinary, data, ordered: true))
                return;
            lock (_sendGate)
            {
                SendItem item = RentItem(ClientTransportProtocol.FrameBinary, data, null, data.Length, null, 0);
                _sendQueue.Enqueue(item);
                Interlocked.Add(ref _bufferedSendBytes, data.Length);
            }
            SignalWake();
        }

        public void SendLatestAsync(byte[] data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            if (_state != WebSocketState.Open || data == null) return;
            bool wake = false;
            lock (_sendGate)
            {
                if (_pendingLatest.TryGetValue(key, out SendItem pending))
                {
                    byte[] prevBytes = pending.Data ?? pending.Deferred?.Encode();
                    byte[] merged = merge == null || prevBytes == null ? data : merge(prevBytes, data);
                    int newBytes = merged != null ? merged.Length : 0;
                    Interlocked.Add(ref _bufferedSendBytes, newBytes - pending.TrackedBytes);
                    pending.Data = merged;
                    pending.Deferred = null;
                    pending.TrackedBytes = newBytes;
                    pending.QueuedAt = Environment.TickCount;
                    pending.MaxAgeMs = maxAgeMs;
                }
                else
                {
                    SendItem item = RentItem(ClientTransportProtocol.FrameBinary, data, null, data.Length, key, maxAgeMs);
                    _sendQueue.Enqueue(item);
                    _pendingLatest.Add(key, item);
                    Interlocked.Add(ref _bufferedSendBytes, data.Length);
                    wake = true;
                }
            }
            if (wake) SignalWake();
        }

        public void SendLatestDeferredAsync(IDeferredNetPayload payload, ulong key, int maxAgeMs = 0)
        {
            if (_state != WebSocketState.Open || payload == null) return;
            bool wake = false;
            lock (_sendGate)
            {
                if (_pendingLatest.TryGetValue(key, out SendItem pending))
                {
                    IDeferredNetPayload merged = pending.Deferred != null ? payload.MergeFrom(pending.Deferred) : payload;
                    int est = Math.Max(0, merged.EstimatedBytes);
                    Interlocked.Add(ref _bufferedSendBytes, est - pending.TrackedBytes);
                    pending.Deferred = merged;
                    pending.Data = null;
                    pending.TrackedBytes = est;
                    pending.QueuedAt = Environment.TickCount;
                    pending.MaxAgeMs = maxAgeMs;
                }
                else
                {
                    int est = Math.Max(0, payload.EstimatedBytes);
                    SendItem item = RentItem(ClientTransportProtocol.FrameBinary, null, payload, est, key, maxAgeMs);
                    _sendQueue.Enqueue(item);
                    _pendingLatest.Add(key, item);
                    Interlocked.Add(ref _bufferedSendBytes, est);
                    wake = true;
                }
            }
            if (wake) SignalWake();
        }

        public void FlushLatest() { }

        private void EnqueueReliable(byte frameType, byte[] data)
        {
            lock (_sendGate)
            {
                _pendingLatest.Clear();
                int len = data != null ? data.Length : 0;
                SendItem item = RentItem(frameType, data, null, len, null, 0);
                _sendQueue.Enqueue(item);
                Interlocked.Add(ref _bufferedSendBytes, len);
            }
            SignalWake();
        }

        private bool TryDequeueSend(out DequeuedFrame frame)
        {
            lock (_sendGate)
            {
                if (!_sendQueue.TryDequeue(out SendItem item))
                {
                    frame = default;
                    return false;
                }
                if (item.Key.HasValue &&
                    _pendingLatest.TryGetValue(item.Key.Value, out SendItem pending) &&
                    ReferenceEquals(pending, item))
                {
                    _pendingLatest.Remove(item.Key.Value);
                }
                if (item.TrackedBytes > 0)
                    Interlocked.Add(ref _bufferedSendBytes, -item.TrackedBytes);
                frame = new DequeuedFrame
                {
                    FrameType = item.FrameType,
                    Data = item.Data,
                    Deferred = item.Deferred,
                    QueuedAt = item.QueuedAt,
                    MaxAgeMs = item.MaxAgeMs
                };
                ReturnItem(item);
                return true;
            }
        }

        private void ClearSendQueue()
        {
            lock (_sendGate)
            {
                while (_sendQueue.TryDequeue(out SendItem item))
                    ReturnItem(item);
                _pendingLatest.Clear();
                Interlocked.Exchange(ref _bufferedSendBytes, 0);
            }
        }

        private SendItem RentItem(byte frameType, byte[] data, IDeferredNetPayload deferred, int trackedBytes, ulong? key, int maxAgeMs)
        {
            SendItem item = _itemPool.Count > 0 ? _itemPool.Pop() : new SendItem();
            item.FrameType = frameType;
            item.Data = data;
            item.Deferred = deferred;
            item.TrackedBytes = trackedBytes;
            item.Key = key;
            item.QueuedAt = Environment.TickCount;
            item.MaxAgeMs = maxAgeMs;
            return item;
        }

        private void ReturnItem(SendItem item)
        {
            if (item == null) return;
            item.Data = null;
            item.Deferred = null;
            item.TrackedBytes = 0;
            item.Key = null;
            if (_itemPool.Count < MaxPooledItems)
                _itemPool.Push(item);
        }

        private void SignalWake()
        {
            if (_wakeSignal.CurrentCount == 0)
            {
                try { _wakeSignal.Release(); }
                catch (SemaphoreFullException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private async Task RunAsync(string host, int port, CancellationToken token)
        {
            ushort closeCode = (ushort)CloseStatusCode.Normal;
            string closeReason = "Normal Closure";

            try
            {
                IPAddress ip = await ResolveIPv4Async(host);
                _remoteEndPoint = new IPEndPoint(ip, port);
                _udp = new UdpClient(ip.AddressFamily);
                ConfigureUdpSocket(_udp);
                _udp.Connect(_remoteEndPoint);

                uint conv = _isBoundMode
                    ? await PerformBindHandshakeAsync(token)
                    : await PerformStandaloneHandshakeAsync(token);

                if (conv == 0 || Volatile.Read(ref _closeRequested) != 0 || token.IsCancellationRequested)
                {
                    closeCode = (ushort)CloseStatusCode.Abnormal;
                    closeReason = _isBoundMode ? "KCP bind handshake failed" : "KCP handshake timeout";
                    if (Volatile.Read(ref _closeRequested) == 0)
                        EnqueueError(closeReason, null);
                    return;
                }

                _conv = conv;
                Volatile.Write(ref _lastReceiveTick, GetMonotonicMs());
                lock (_kcpLock)
                {
                    _kcp = new ClientKcpCore(conv, OnKcpOutput);
                    _kcp.SetNoDelay(1, 10, 2, 1);
                    _kcp.SetWndSize(128, 128);
                    _kcp.SetMtu(1200);
                    _kcp.Update(unchecked((uint)GetMonotonicMs()));
                }

                _state = WebSocketState.Open;
                _eventQueue.Enqueue(new OpenEventArgs());

                Task tickTask = RunKcpLoopAsync(token);
                (closeCode, closeReason) = await RunReceiveLoopAsync(token);

                try { _cts?.Cancel(); } catch { }
                try { await tickTask; } catch { }
            }
            catch (OperationCanceledException)
            {
                closeCode = (ushort)CloseStatusCode.Normal;
                closeReason = "Normal Closure";
            }
            catch (Exception ex)
            {
                if (Volatile.Read(ref _closeRequested) == 0)
                {
                    EnqueueError(ex.Message, ex);
                    closeCode = (ushort)CloseStatusCode.Abnormal;
                    closeReason = ex.Message;
                }
            }
            finally
            {
                _state = WebSocketState.Closed;
                CleanupSocket();
                EnqueueClose(closeCode, closeReason);
            }
        }

        private async Task<uint> PerformStandaloneHandshakeAsync(CancellationToken token)
        {
            uint nonce = unchecked((uint)Guid.NewGuid().GetHashCode());
            if (nonce == 0) nonce = 1;
            _authTag = nonce;

            byte[] req = new byte[5];
            req[0] = ClientTransportProtocol.UdpConnectReq;
            ClientTransportProtocol.WriteUInt32LE(req, 1, nonce);

            long deadline = GetMonotonicMs() + StandaloneHandshakeTimeoutMs;
            Task<UdpReceiveResult> recvTask = _udp.ReceiveAsync();
            while (!token.IsCancellationRequested && GetMonotonicMs() < deadline)
            {
                await _udp.SendAsync(req, req.Length);
                Task delayTask = Task.Delay(HandshakeRetryMs, token);
                Task completed = await Task.WhenAny(recvTask, delayTask);
                if (completed == recvTask)
                {
                    UdpReceiveResult result = await recvTask;
                    byte[] buf = result.Buffer;
                    if (buf.Length >= 9 &&
                        buf[0] == ClientTransportProtocol.UdpConnectAck &&
                        ClientTransportProtocol.ReadUInt32LE(buf, 1) == nonce)
                    {
                        return ClientTransportProtocol.ReadUInt32LE(buf, 5);
                    }
                    recvTask = _udp.ReceiveAsync();
                }
            }

            ObserveAbandonedReceive(recvTask);
            return 0;
        }

        private async Task<uint> PerformBindHandshakeAsync(CancellationToken token)
        {
            if (_bindToken == null || _bindToken.Length < ClientTransportProtocol.BindTokenBytes)
                return 0;

            byte[] req = new byte[5 + ClientTransportProtocol.BindTokenBytes];
            req[0] = ClientTransportProtocol.UdpBindReq;
            ClientTransportProtocol.WriteUInt32LE(req, 1, _boundConv);
            Buffer.BlockCopy(_bindToken, 0, req, 5, ClientTransportProtocol.BindTokenBytes);

            long deadline = GetMonotonicMs() + BindHandshakeTimeoutMs;
            Task<UdpReceiveResult> recvTask = _udp.ReceiveAsync();
            while (!token.IsCancellationRequested && GetMonotonicMs() < deadline)
            {
                await _udp.SendAsync(req, req.Length);
                Task delayTask = Task.Delay(HandshakeRetryMs, token);
                Task completed = await Task.WhenAny(recvTask, delayTask);
                if (completed == recvTask)
                {
                    UdpReceiveResult result = await recvTask;
                    byte[] buf = result.Buffer;
                    if (buf.Length >= 5 && ClientTransportProtocol.ReadUInt32LE(buf, 1) == _boundConv)
                    {
                        if (buf[0] == ClientTransportProtocol.UdpBindAck)
                            return _boundConv;
                        if (buf[0] == ClientTransportProtocol.UdpBindReject)
                            return 0;
                    }
                    recvTask = _udp.ReceiveAsync();
                }
            }

            ObserveAbandonedReceive(recvTask);
            return 0;
        }

        private static void ObserveAbandonedReceive(Task<UdpReceiveResult> task)
        {
            if (task == null) return;
            task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task RunKcpLoopAsync(CancellationToken token)
        {
            long lastPingTick = GetMonotonicMs();
            try
            {
                while (!token.IsCancellationRequested && Volatile.Read(ref _closeRequested) == 0)
                {
                    long now = GetMonotonicMs();
                    if (now - Volatile.Read(ref _lastReceiveTick) > IdleTimeoutMs)
                    {
                        EnqueueError("KCP 连接超时。", null);
                        AbortInternal();
                        return;
                    }

                    bool dead;
                    int waitMs = 10;
                    lock (_kcpLock)
                    {
                        if (_kcp == null) return;
                        bool sentAny = false;
                        while (TryDequeueSend(out DequeuedFrame item))
                        {
                            if (item.MaxAgeMs > 0 && unchecked(Environment.TickCount - item.QueuedAt) > item.MaxAgeMs)
                                continue;
                            byte[] framePayload = item.Data ?? item.Deferred?.Encode();
                            // 带有效期的可丢帧快照（如位置同步）走非可靠 UDP 且 seq=0（由上层按 ObjectId 独立去重），避免跨对象乱序误杀。
                            if (item.MaxAgeMs > 0 && TrySendUnreliableDirect(item.FrameType, framePayload, ordered: false))
                                continue;
                            SendKcpFrameLocked(item.FrameType, framePayload);
                            sentAny = true;
                        }

                        if (now - lastPingTick >= PingIntervalMs)
                        {
                            lastPingTick = now;
                            SendKcpFrameLocked(ClientTransportProtocol.FramePing, Array.Empty<byte>());
                            sentAny = true;
                        }

                        uint nowU32 = unchecked((uint)now);
                        _kcp.Update(nowU32);
                        if (sentAny)
                            _kcp.Flush();

                        dead = _kcp.IsDead;
                        if (!dead)
                        {
                            uint nextTick = _kcp.Check(nowU32);
                            int diff = unchecked((int)(nextTick - nowU32));
                            if (diff < 1) waitMs = 1;
                            else if (diff > 50) waitMs = 50;
                            else waitMs = diff;
                        }
                    }

                    if (dead)
                    {
                        EnqueueError("KCP 链路已断开。", null);
                        AbortInternal();
                        return;
                    }

                    await _wakeSignal.WaitAsync(waitMs, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                if (Volatile.Read(ref _closeRequested) == 0)
                    EnqueueError(ex.Message, ex);
                AbortInternal();
            }
        }

        private async Task<(ushort Code, string Reason)> RunReceiveLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult received;
                try
                {
                    received = await _udp.ReceiveAsync();
                }
                catch (ObjectDisposedException)
                {
                    return Volatile.Read(ref _closeRequested) != 0
                        ? ((ushort)CloseStatusCode.Normal, "Normal Closure")
                        : ((ushort)CloseStatusCode.Abnormal, "KCP socket closed");
                }
                catch (SocketException ex)
                {
                    if (Volatile.Read(ref _closeRequested) != 0 || token.IsCancellationRequested)
                        return ((ushort)CloseStatusCode.Normal, "Normal Closure");
                    return ((ushort)CloseStatusCode.Abnormal, ex.Message);
                }

                byte[] buf = received.Buffer;
                if (buf == null || buf.Length < 1) continue;

                Volatile.Write(ref _lastReceiveTick, GetMonotonicMs());
                byte kind = buf[0];
                if (kind == ClientTransportProtocol.UdpClose && buf.Length >= 7)
                {
                    uint conv = ClientTransportProtocol.ReadUInt32LE(buf, 1);
                    if (conv == _conv)
                    {
                        ushort code = ClientTransportProtocol.ReadUInt16LE(buf, 5);
                        return (code, "Remote closed KCP");
                    }
                    continue;
                }

                if (kind == ClientTransportProtocol.UdpUnreliableData)
                {
                    HandleReceivedUnreliableDatagram(buf);
                    continue;
                }

                if (kind != ClientTransportProtocol.UdpKcpData || buf.Length < 1 + ClientKcpCore.Overhead)
                    continue;

                uint segConv = ClientKcpCore.ReadConv(buf, 1, buf.Length - 1);
                if (segConv != _conv) continue;

                ushort? remoteCloseCode = null;
                string remoteCloseReason = null;
                lock (_kcpLock)
                {
                    if (_kcp == null) break;
                    if (_kcp.Input(buf, 1, buf.Length - 1) < 0) continue;

                    while (true)
                    {
                        int size = _kcp.PeekSize();
                        if (size <= 0) break;
                        if (size > MaxKcpMessageBytes)
                        {
                            remoteCloseCode = (ushort)CloseStatusCode.TooBig;
                            remoteCloseReason = "KCP frame too large";
                            break;
                        }

                        if (_recvScratch.Length < size)
                            _recvScratch = new byte[size];

                        int read = _kcp.Recv(_recvScratch);
                        if (read <= 0) break;

                        byte frameType = _recvScratch[0];
                        int payloadLen = read - 1;
                        switch (frameType)
                        {
                            case ClientTransportProtocol.FrameBinary:
                            {
                                if (payloadLen > 0)
                                {
                                    byte[] rented = ArrayPool<byte>.Shared.Rent(payloadLen);
                                    Buffer.BlockCopy(_recvScratch, 1, rented, 0, payloadLen);
                                    _eventQueue.Enqueue(MessageEventArgs.CreatePooledBinary(rented, payloadLen));
                                }
                                else
                                {
                                    _eventQueue.Enqueue(MessageEventArgs.CreateBinary(Array.Empty<byte>()));
                                }
                                break;
                            }
                            case ClientTransportProtocol.FrameText:
                            {
                                byte[] payload = payloadLen > 0 ? new byte[payloadLen] : Array.Empty<byte>();
                                if (payloadLen > 0) Buffer.BlockCopy(_recvScratch, 1, payload, 0, payloadLen);
                                _eventQueue.Enqueue(MessageEventArgs.CreateText(payload));
                                break;
                            }
                            case ClientTransportProtocol.FramePing:
                            {
                                _recvScratch[0] = ClientTransportProtocol.FramePong;
                                _kcp.Send(_recvScratch, 0, read);
                                break;
                            }
                            case ClientTransportProtocol.FramePong:
                                break;
                            case ClientTransportProtocol.FrameClose:
                            {
                                ClientTransportProtocol.ParseClosePayload(_recvScratch, 1, payloadLen, out ushort code, out string reason);
                                remoteCloseCode = code;
                                remoteCloseReason = reason;
                                break;
                            }
                        }

                        if (_recvScratch.Length > MaxReusableBufBytes)
                            _recvScratch = new byte[4096];

                        if (remoteCloseCode.HasValue)
                            break;
                    }

                    _kcp.Flush();
                }
                SignalWake();

                if (remoteCloseCode.HasValue)
                    return (remoteCloseCode.Value, remoteCloseReason ?? string.Empty);
            }

            return ((ushort)CloseStatusCode.Normal, "Normal Closure");
        }

        private bool TrySendUnreliableDirect(byte frameType, byte[] payload, bool ordered)
        {
            UdpClient udp = _udp;
            uint conv = _conv;
            if (udp == null || conv == 0 || frameType != ClientTransportProtocol.FrameBinary)
                return false;

            int payloadLen = payload != null ? payload.Length : 0;
            if (payloadLen > ClientTransportProtocol.SafeUnreliablePayloadBytes)
                return false;

            uint seq = 0;
            if (ordered)
            {
                seq = unchecked((uint)Interlocked.Increment(ref UnsafeSeqCounter));
                if (seq == 0) seq = unchecked((uint)Interlocked.Increment(ref UnsafeSeqCounter));
            }
            _ = _unreliableOutSeq;

            int total = ClientTransportProtocol.UnreliableHeaderBytes + payloadLen;
            lock (_unreliableSendLock)
            {
                _unreliableSendBuf[0] = ClientTransportProtocol.UdpUnreliableData;
                ClientTransportProtocol.WriteUInt32LE(_unreliableSendBuf, 1, conv);
                ClientTransportProtocol.WriteUInt32LE(_unreliableSendBuf, 5, _authTag);
                ClientTransportProtocol.WriteUInt32LE(_unreliableSendBuf, 9, seq);
                _unreliableSendBuf[13] = frameType;
                if (payloadLen > 0)
                    Buffer.BlockCopy(payload, 0, _unreliableSendBuf, ClientTransportProtocol.UnreliableHeaderBytes, payloadLen);
                try
                {
                    udp.Send(_unreliableSendBuf, total);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private int UnsafeSeqCounter;

        private void HandleReceivedUnreliableDatagram(byte[] buf)
        {
            if (buf.Length < ClientTransportProtocol.UnreliableHeaderBytes)
                return;
            uint conv = ClientTransportProtocol.ReadUInt32LE(buf, 1);
            if (conv != _conv)
                return;
            uint tag = ClientTransportProtocol.ReadUInt32LE(buf, 5);
            if (_authTag != 0 && tag != _authTag)
                return;
            uint seq = ClientTransportProtocol.ReadUInt32LE(buf, 9);
            if (seq != 0)
            {
                if (_hasUnreliableInSeq && unchecked((int)(seq - _unreliableInMaxSeq)) <= 0)
                    return;
                _unreliableInMaxSeq = seq;
                _hasUnreliableInSeq = true;
            }

            byte frameType = buf[13];
            int payloadLen = buf.Length - ClientTransportProtocol.UnreliableHeaderBytes;
            if (frameType == ClientTransportProtocol.FrameBinary)
            {
                if (payloadLen > 0)
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent(payloadLen);
                    Buffer.BlockCopy(buf, ClientTransportProtocol.UnreliableHeaderBytes, rented, 0, payloadLen);
                    _eventQueue.Enqueue(MessageEventArgs.CreatePooledBinary(rented, payloadLen));
                }
                else
                {
                    _eventQueue.Enqueue(MessageEventArgs.CreateBinary(Array.Empty<byte>()));
                }
            }
        }

        private void SendKcpFrameLocked(byte frameType, byte[] payload)
        {
            if (_kcp == null) return;
            int payloadLen = payload != null ? payload.Length : 0;
            int total = 1 + payloadLen;
            if (total > MaxKcpMessageBytes)
                return;

            if (_sendScratch.Length < total)
                _sendScratch = new byte[total];

            _sendScratch[0] = frameType;
            if (payloadLen > 0)
                Buffer.BlockCopy(payload, 0, _sendScratch, 1, payloadLen);
            _kcp.Send(_sendScratch, 0, total);

            if (_sendScratch.Length > MaxReusableBufBytes)
                _sendScratch = new byte[4096];
        }

        private void OnKcpOutput(byte[] buffer, int length)
        {
            UdpClient udp = _udp;
            if (udp == null || length <= 0) return;

            int total = 1 + length;
            if (_udpOutBuf.Length < total)
                _udpOutBuf = new byte[total];

            _udpOutBuf[0] = ClientTransportProtocol.UdpKcpData;
            Buffer.BlockCopy(buffer, 0, _udpOutBuf, 1, length);
            try
            {
                udp.Send(_udpOutBuf, total);
            }
            catch { }
        }

        private void SendCloseNotification(ushort code, string reason)
        {
            if (_conv == 0 || _udp == null) return;
            try
            {
                lock (_kcpLock)
                {
                    if (_kcp != null)
                    {
                        byte[] closePayload = ClientTransportProtocol.BuildClosePayload(code, reason);
                        SendKcpFrameLocked(ClientTransportProtocol.FrameClose, closePayload);
                        _kcp.Flush();
                    }
                }

                byte[] udpClose = new byte[7];
                udpClose[0] = ClientTransportProtocol.UdpClose;
                ClientTransportProtocol.WriteUInt32LE(udpClose, 1, _conv);
                ClientTransportProtocol.WriteUInt16LE(udpClose, 5, code);
                _udp.Send(udpClose, udpClose.Length);
            }
            catch { }
        }

        private static async Task<IPAddress> ResolveIPv4Async(string host)
        {
            if (IPAddress.TryParse(host, out IPAddress parsed))
                return parsed;

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                    return addresses[i];
            }
            if (addresses.Length > 0)
                return addresses[0];

            throw new SocketException((int)SocketError.HostNotFound);
        }

        private static void ConfigureUdpSocket(UdpClient udp)
        {
            try
            {
                udp.Client.ReceiveBufferSize = UdpSocketBufferBytes;
                udp.Client.SendBufferSize = UdpSocketBufferBytes;
            }
            catch { }

            try
            {
                const int sioUdpConnReset = -1744830452;
                udp.Client.IOControl((IOControlCode)sioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch { }
        }

        private static long GetMonotonicMs()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp() * 1000L / System.Diagnostics.Stopwatch.Frequency;
        }

        private void EnqueueError(string message, Exception ex)
        {
            _eventQueue.Enqueue(new ErrorEventArgs(message, ex));
        }

        private void EnqueueClose(ushort code, string reason)
        {
            if (Interlocked.Exchange(ref _closeEnqueued, 1) == 0)
                _eventQueue.Enqueue(new CloseEventArgs(code, reason));
        }

        private void AbortInternal()
        {
            SignalWake();
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
        }

        private void CleanupSocket()
        {
            ClearSendQueue();
            lock (_kcpLock)
            {
                _kcp?.Clear();
                _kcp = null;
            }
            try { _udp?.Dispose(); } catch { }
            _udp = null;
        }

        void IMainThreadPumpable.PumpMainThreadEvents()
        {
            while (_eventQueue.TryDequeue(out EventArgs e))
            {
                if (e is OpenEventArgs openArgs)
                {
                    OnOpen?.Invoke(this, openArgs);
                }
                else if (e is MessageEventArgs msgArgs)
                {
                    OnMessage?.Invoke(this, msgArgs);
                }
                else if (e is ErrorEventArgs errArgs)
                {
                    OnError?.Invoke(this, errArgs);
                }
                else if (e is CloseEventArgs closeArgs)
                {
                    OnClose?.Invoke(this, closeArgs);
                    NetTransportPump.Unregister(this);
                    while (_eventQueue.TryDequeue(out EventArgs dropped))
                    {
                        if (dropped is MessageEventArgs droppedMsg)
                            droppedMsg.ReleasePooledBuffer();
                    }
                    break;
                }
            }
        }

        void IMainThreadPumpable.AbortOnQuit()
        {
            Interlocked.Exchange(ref _closeRequested, 1);
            _state = WebSocketState.Closed;
            AbortInternal();
            CleanupSocket();
        }
    }
}
#endif
