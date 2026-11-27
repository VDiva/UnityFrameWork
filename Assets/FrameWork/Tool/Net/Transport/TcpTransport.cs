#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityWebSocket;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 原生平台 TCP 传输层（4 字节小端长度前缀 + 1 字节帧类型 + 载荷）。
    /// 支持 TCP_NODELAY、64KB 套接字缓冲、16KB 读缓冲批解析、32KB 写合并批发送与 SendItem 池化复用。
    /// </summary>
    public sealed class TcpTransport : INetTransport, IMainThreadPumpable
    {
        private const int DefaultTcpPort = 5101;
        private const int PingIntervalMs = 5000;
        private const int SocketBufferBytes = 64 * 1024;
        private const int ReadBufBytes = 16 * 1024;
        private const int BatchFlushBytes = 32 * 1024;
        private const int MaxReusableBufBytes = 64 * 1024;
        private const int MaxPooledItems = 256;

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

        private sealed class BindInfoEventArgs : EventArgs
        {
            public readonly uint Conv;
            public readonly byte[] Token;

            public BindInfoEventArgs(uint conv, byte[] token)
            {
                Conv = conv;
                Token = token;
            }
        }

        private readonly object _sendGate = new object();
        private readonly ConcurrentQueue<SendItem> _sendQueue = new ConcurrentQueue<SendItem>();
        private readonly Dictionary<ulong, SendItem> _pendingLatest = new Dictionary<ulong, SendItem>();
        private readonly Stack<SendItem> _itemPool = new Stack<SendItem>();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0, 1);
        private readonly ConcurrentQueue<EventArgs> _eventQueue = new ConcurrentQueue<EventArgs>();
        private readonly byte[] _readBuf = new byte[ReadBufBytes];
        private int _readPos;
        private int _readLen;
        private byte[] _writeBatchBuf = new byte[8192];

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private volatile WebSocketState _state = WebSocketState.Closed;
        private int _bufferedSendBytes;
        private int _closeRequested;
        private int _closeEnqueued;
        private int _started;

        public string Address { get; }
        public string TransportName => "TCP";
        public bool IsRealtimeChannelActive => false;
        public WebSocketState ReadyState => _state;
        public int PendingSendCount => _sendQueue.Count;
        public int BufferedSendBytes => Volatile.Read(ref _bufferedSendBytes);

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<UnityWebSocket.ErrorEventArgs> OnError;
        public event EventHandler<MessageEventArgs> OnMessage;
        internal event Action<uint, byte[]> OnBindInfoReceived;

        public TcpTransport(string address)
        {
            Address = address;
        }

        public void ConnectAsync()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                EnqueueError("TCP socket is already active.", null);
                return;
            }

            if (!ClientTransportProtocol.TryParseEndpoint(Address, DefaultTcpPort, out string host, out int port))
            {
                _state = WebSocketState.Closed;
                EnqueueError($"无效的 TCP 地址: {Address}", null);
                EnqueueClose((ushort)CloseStatusCode.Abnormal, "Invalid TCP address");
                return;
            }

            _state = WebSocketState.Connecting;
            _cts = new CancellationTokenSource();
            NetTransportPump.Register(this);
            Task.Run(() => ConnectAndRunAsync(host, port, _cts.Token));
        }

        public void CloseAsync()
        {
            if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
                return;

            if (_state == WebSocketState.Open)
                _state = WebSocketState.Closing;

            SignalSend();
            if (_state == WebSocketState.Connecting)
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
            EnqueueStream(ClientTransportProtocol.FrameBinary, data);
        }

        public void SendLatestAsync(byte[] data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            if (_state != WebSocketState.Open || data == null) return;
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
                    SignalSend();
                }
            }
        }

        public void SendLatestDeferredAsync(IDeferredNetPayload payload, ulong key, int maxAgeMs = 0)
        {
            if (_state != WebSocketState.Open || payload == null) return;
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
                    SignalSend();
                }
            }
        }

        public void FlushLatest() { }

        private void EnqueueReliable(byte frameType, byte[] data)
        {
            lock (_sendGate)
            {
                // 可靠消息形成顺序屏障，后续实时状态不能覆盖其之前的待发状态。
                _pendingLatest.Clear();
                int len = data != null ? data.Length : 0;
                SendItem item = RentItem(frameType, data, null, len, null, 0);
                _sendQueue.Enqueue(item);
                Interlocked.Add(ref _bufferedSendBytes, len);
                SignalSend();
            }
        }

        private void EnqueueStream(byte frameType, byte[] data)
        {
            lock (_sendGate)
            {
                int len = data != null ? data.Length : 0;
                SendItem item = RentItem(frameType, data, null, len, null, 0);
                _sendQueue.Enqueue(item);
                Interlocked.Add(ref _bufferedSendBytes, len);
                SignalSend();
            }
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

        private void SignalSend()
        {
            if (_sendSignal.CurrentCount == 0)
            {
                try { _sendSignal.Release(); }
                catch (SemaphoreFullException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private async Task ConnectAndRunAsync(string host, int port, CancellationToken token)
        {
            ushort closeCode = (ushort)CloseStatusCode.Normal;
            string closeReason = "Normal Closure";
            try
            {
                _client = new TcpClient();
                _client.NoDelay = true;
                try
                {
                    _client.SendBufferSize = SocketBufferBytes;
                    _client.ReceiveBufferSize = SocketBufferBytes;
                }
                catch { }
                _readPos = 0;
                _readLen = 0;
                var connectTask = _client.ConnectAsync(host, port);
                var cancelTask = Task.Delay(Timeout.Infinite, token);
                if (await Task.WhenAny(connectTask, cancelTask) == cancelTask)
                    throw new OperationCanceledException(token);
                await connectTask;

                if (Volatile.Read(ref _closeRequested) != 0 || token.IsCancellationRequested)
                {
                    AbortInternal();
                    EnqueueClose((ushort)CloseStatusCode.Normal, "Closed while connecting");
                    return;
                }

                _stream = _client.GetStream();
                _state = WebSocketState.Open;
                _eventQueue.Enqueue(new OpenEventArgs());

                Task sendTask = RunSendLoopAsync(token);
                (closeCode, closeReason) = await RunReceiveLoopAsync(token);

                try { _cts?.Cancel(); } catch { }
                SignalSend();
                try { await sendTask; } catch { }
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

        private async Task RunSendLoopAsync(CancellationToken token)
        {
            int lastWriteTick = Environment.TickCount;
            try
            {
                while (Volatile.Read(ref _closeRequested) == 0 && !token.IsCancellationRequested)
                {
                    bool signaled = await _sendSignal.WaitAsync(PingIntervalMs, token);
                    if (Volatile.Read(ref _closeRequested) != 0) break;

                    bool wroteAny = false;
                    int batchOffset = 0;
                    while (Volatile.Read(ref _closeRequested) == 0 && TryDequeueSend(out DequeuedFrame item))
                    {
                        if (item.MaxAgeMs > 0 && unchecked(Environment.TickCount - item.QueuedAt) > item.MaxAgeMs)
                            continue;

                        byte[] framePayload = item.Data ?? item.Deferred?.Encode();
                        int payloadLen = framePayload != null ? framePayload.Length : 0;
                        int frameBytes = 5 + payloadLen;
                        if (batchOffset > 0 && batchOffset + frameBytes > BatchFlushBytes)
                        {
                            await FlushBatchBufferAsync(batchOffset, token);
                            batchOffset = 0;
                            lastWriteTick = Environment.TickCount;
                            wroteAny = true;
                        }

                        EnsureWriteBatchCapacity(batchOffset + frameBytes);
                        ClientTransportProtocol.WriteInt32LE(_writeBatchBuf, batchOffset, payloadLen);
                        _writeBatchBuf[batchOffset + 4] = item.FrameType;
                        if (payloadLen > 0)
                            Buffer.BlockCopy(framePayload, 0, _writeBatchBuf, batchOffset + 5, payloadLen);
                        batchOffset += frameBytes;
                    }

                    if (batchOffset > 0)
                    {
                        await FlushBatchBufferAsync(batchOffset, token);
                        lastWriteTick = Environment.TickCount;
                        wroteAny = true;
                    }

                    if (_writeBatchBuf.Length > MaxReusableBufBytes)
                        _writeBatchBuf = new byte[8192];

                    if (!wroteAny && !signaled && unchecked(Environment.TickCount - lastWriteTick) >= PingIntervalMs)
                    {
                        await WriteSingleFrameAsync(ClientTransportProtocol.FramePing, Array.Empty<byte>(), token);
                        lastWriteTick = Environment.TickCount;
                    }
                }

                if (Volatile.Read(ref _closeRequested) != 0 && _stream != null)
                {
                    ClearSendQueue();
                    byte[] payload = ClientTransportProtocol.BuildClosePayload((ushort)CloseStatusCode.Normal, "Normal Closure");
                    try
                    {
                        using (var closeCts = new CancellationTokenSource(1000))
                        {
                            await WriteSingleFrameAsync(ClientTransportProtocol.FrameClose, payload, closeCts.Token);
                        }
                    }
                    catch { }
                    try { _client?.Client?.Shutdown(SocketShutdown.Both); } catch { }
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
                if (!await EnsureBufferedAsync(5, token))
                {
                    return Volatile.Read(ref _closeRequested) != 0
                        ? ((ushort)CloseStatusCode.Normal, "Normal Closure")
                        : ((ushort)CloseStatusCode.Abnormal, "Remote closed TCP connection");
                }

                int payloadLength = ClientTransportProtocol.ReadInt32LE(_readBuf, _readPos);
                byte frameType = _readBuf[_readPos + 4];
                ConsumeReadBuffer(5);

                if (payloadLength < 0 || payloadLength > ClientTransportProtocol.MaxFrameBytes)
                    throw new InvalidDataException($"Invalid TCP frame length: {payloadLength}");

                if (frameType == ClientTransportProtocol.FrameBinary)
                {
                    if (payloadLength == 0)
                    {
                        _eventQueue.Enqueue(MessageEventArgs.CreateBinary(Array.Empty<byte>()));
                    }
                    else
                    {
                        byte[] rented = ArrayPool<byte>.Shared.Rent(payloadLength);
                        if (!await ReadExactBufferedAsync(rented, payloadLength, token))
                        {
                            ArrayPool<byte>.Shared.Return(rented);
                            return ((ushort)CloseStatusCode.Abnormal, "Unexpected TCP EOF");
                        }
                        _eventQueue.Enqueue(MessageEventArgs.CreatePooledBinary(rented, payloadLength));
                    }
                    continue;
                }

                byte[] payload = payloadLength > 0 ? new byte[payloadLength] : Array.Empty<byte>();
                if (payloadLength > 0 && !await ReadExactBufferedAsync(payload, payloadLength, token))
                {
                    return ((ushort)CloseStatusCode.Abnormal, "Unexpected TCP EOF");
                }

                switch (frameType)
                {
                    case ClientTransportProtocol.FrameText:
                        _eventQueue.Enqueue(MessageEventArgs.CreateText(payload));
                        break;
                    case ClientTransportProtocol.FramePing:
                        EnqueueReliable(ClientTransportProtocol.FramePong, payload);
                        break;
                    case ClientTransportProtocol.FramePong:
                        break;
                    case ClientTransportProtocol.FrameBindInfo:
                        if (payload.Length >= 4 + ClientTransportProtocol.BindTokenBytes)
                        {
                            uint conv = ClientTransportProtocol.ReadUInt32LE(payload, 0);
                            byte[] bindToken = new byte[ClientTransportProtocol.BindTokenBytes];
                            Buffer.BlockCopy(payload, 4, bindToken, 0, ClientTransportProtocol.BindTokenBytes);
                            _eventQueue.Enqueue(new BindInfoEventArgs(conv, bindToken));
                        }
                        break;
                    case ClientTransportProtocol.FrameClose:
                        ClientTransportProtocol.ParseClosePayload(payload, 0, payload.Length, out ushort code, out string reason);
                        return (code, reason);
                }
            }

            return ((ushort)CloseStatusCode.Normal, "Normal Closure");
        }

        private void EnsureWriteBatchCapacity(int required)
        {
            if (_writeBatchBuf.Length >= required) return;
            int newSize = _writeBatchBuf.Length * 2;
            if (newSize < required) newSize = required;
            var next = new byte[newSize];
            Buffer.BlockCopy(_writeBatchBuf, 0, next, 0, _writeBatchBuf.Length);
            _writeBatchBuf = next;
        }

        private Task FlushBatchBufferAsync(int count, CancellationToken token)
        {
            NetworkStream stream = _stream;
            if (stream == null) throw new IOException("TCP stream is not open.");
            return stream.WriteAsync(_writeBatchBuf, 0, count, token);
        }

        private Task WriteSingleFrameAsync(byte frameType, byte[] payload, CancellationToken token)
        {
            int payloadLen = payload != null ? payload.Length : 0;
            EnsureWriteBatchCapacity(5 + payloadLen);
            ClientTransportProtocol.WriteInt32LE(_writeBatchBuf, 0, payloadLen);
            _writeBatchBuf[4] = frameType;
            if (payloadLen > 0)
                Buffer.BlockCopy(payload, 0, _writeBatchBuf, 5, payloadLen);
            return FlushBatchBufferAsync(5 + payloadLen, token);
        }

        private void ConsumeReadBuffer(int count)
        {
            _readPos += count;
            if (_readPos >= _readLen)
            {
                _readPos = 0;
                _readLen = 0;
            }
        }

        private async Task<bool> EnsureBufferedAsync(int required, CancellationToken token)
        {
            NetworkStream stream = _stream;
            if (stream == null) return false;

            int available = _readLen - _readPos;
            if (available >= required) return true;

            if (_readPos > 0)
            {
                if (available > 0)
                    Buffer.BlockCopy(_readBuf, _readPos, _readBuf, 0, available);
                _readPos = 0;
                _readLen = available;
            }

            while (_readLen < required)
            {
                int read = await stream.ReadAsync(_readBuf, _readLen, _readBuf.Length - _readLen, token);
                if (read <= 0) return false;
                _readLen += read;
            }
            return true;
        }

        private async Task<bool> ReadExactBufferedAsync(byte[] destination, int count, CancellationToken token)
        {
            NetworkStream stream = _stream;
            if (stream == null) return false;

            int offset = 0;
            int buffered = _readLen - _readPos;
            if (buffered > 0)
            {
                int take = Math.Min(buffered, count);
                Buffer.BlockCopy(_readBuf, _readPos, destination, 0, take);
                ConsumeReadBuffer(take);
                offset += take;
            }

            while (offset < count)
            {
                int remaining = count - offset;
                if (remaining >= ReadBufBytes / 2)
                {
                    int read = await stream.ReadAsync(destination, offset, remaining, token);
                    if (read <= 0) return false;
                    offset += read;
                }
                else
                {
                    int read = await stream.ReadAsync(_readBuf, 0, _readBuf.Length, token);
                    if (read <= 0) return false;
                    _readPos = 0;
                    _readLen = read;
                    int take = Math.Min(read, remaining);
                    Buffer.BlockCopy(_readBuf, 0, destination, offset, take);
                    ConsumeReadBuffer(take);
                    offset += take;
                }
            }
            return true;
        }

        private void EnqueueError(string message, Exception ex)
        {
            _eventQueue.Enqueue(new UnityWebSocket.ErrorEventArgs(message, ex));
        }

        private void EnqueueClose(ushort code, string reason)
        {
            if (Interlocked.Exchange(ref _closeEnqueued, 1) == 0)
                _eventQueue.Enqueue(new CloseEventArgs(code, reason));
        }

        private void AbortInternal()
        {
            try { _cts?.Cancel(); } catch { }
            try { _client?.Close(); } catch { }
        }

        private void CleanupSocket()
        {
            ClearSendQueue();
            try { _stream?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
            _stream = null;
            _client = null;
        }

        void IMainThreadPumpable.PumpMainThreadEvents()
        {
            while (_eventQueue.TryDequeue(out EventArgs e))
            {
                if (e is OpenEventArgs openArgs)
                {
                    OnOpen?.Invoke(this, openArgs);
                }
                else if (e is BindInfoEventArgs bindArgs)
                {
                    OnBindInfoReceived?.Invoke(bindArgs.Conv, bindArgs.Token);
                }
                else if (e is MessageEventArgs msgArgs)
                {
                    OnMessage?.Invoke(this, msgArgs);
                }
                else if (e is UnityWebSocket.ErrorEventArgs errArgs)
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
