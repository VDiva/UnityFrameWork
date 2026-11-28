using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WebSocketDemo.Transport
{
    /// <summary>
    /// 服务端 KCP 传输层（基于共享 UdpClient 复用端口，按 conv 路由会话）。
    /// 性能优化：KcpCore.Check() 懒调度、复用的 UDP 出站/收包缓冲与 ArrayPool 零拷贝发送。
    /// </summary>
    public sealed class KcpClientTransport : IClientTransport
    {
        private const int MaxKcpMessageBytes = 256 * 1024;
        private const long IdleTimeoutMs = 25_000;

        private readonly object _kcpLock = new();
        private readonly KcpCore _kcp;
        private readonly Action<IPEndPoint, byte[], int> _udpSend;
        private readonly Action<uint> _onClosed;
        private readonly Channel<TransportReceiveResult> _incoming = Channel.CreateBounded<TransportReceiveResult>(
            new BoundedChannelOptions(512) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });

        // 以下两个缓冲区仅在持有 _kcpLock 时读写，避免高频收发反复分配 byte[]。
        private byte[] _udpOutBuffer = new byte[2048];
        private byte[] _recvScratch = new byte[4096];

        private IPEndPoint _remoteEndPoint;
        private readonly uint _authTag;
        private uint _unreliableOutSeq;
        private uint _unreliableInMaxSeq;
        private bool _hasUnreliableInSeq;
        private long _lastReceiveTick = Environment.TickCount64;
        private uint _nextUpdateMs;
        private int _closed;
        private int _disposed;

        public uint Conv { get; }
        public string TransportName => "KCP";
        public bool IsConnected => Volatile.Read(ref _closed) == 0 && !_kcp.IsDead;

        public KcpClientTransport(
            uint conv,
            IPEndPoint remoteEndPoint,
            Action<IPEndPoint, byte[], int> udpSend,
            Action<uint> onClosed,
            uint authTag = 0)
        {
            Conv = conv;
            _authTag = authTag;
            _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _udpSend = udpSend ?? throw new ArgumentNullException(nameof(udpSend));
            _onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));

            _kcp = new KcpCore(conv, OnKcpOutput);
            _kcp.SetNoDelay(1, 10, 2, 1);
            _kcp.SetWndSize(128, 128);
            _kcp.SetMtu(1200);
            uint now = CurrentClock();
            _kcp.Update(now);
            _nextUpdateMs = _kcp.Check(now);
        }

        public void OnUdpData(ReadOnlySpan<byte> kcpSegment, IPEndPoint remoteEndPoint)
        {
            if (Volatile.Read(ref _closed) != 0) return;

            Volatile.Write(ref _lastReceiveTick, Environment.TickCount64);
            lock (_kcpLock)
            {
                if (Volatile.Read(ref _closed) != 0) return;
                // 支持 NAT 端口重绑定 / Wi-Fi 与蜂窝切换。
                if (!Equals(_remoteEndPoint, remoteEndPoint))
                    _remoteEndPoint = remoteEndPoint;

                if (_kcp.Input(kcpSegment) < 0) return;
                DrainReceivedMessagesLocked();
                _kcp.Flush();
                Volatile.Write(ref _nextUpdateMs, _kcp.Check(CurrentClock()));
            }
        }

        public void OnRemoteUdpClose(WebSocketCloseStatus status)
        {
            CloseInternal(status, "Remote closed", sendUdpClose: false);
        }

        /// <summary>
        /// 接收非可靠有序 UDP 数据报（跳过 KCP 重传缓冲，消除弱网丢包时的队头阻塞）。
        /// </summary>
        public void OnUdpUnreliableData(ReadOnlySpan<byte> bodyAfterConv, IPEndPoint remoteEndPoint)
        {
            if (Volatile.Read(ref _closed) != 0) return;
            // bodyAfterConv: [4B authTag][4B seq][1B frameType][payload...]
            if (bodyAfterConv.Length < 9) return;

            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(bodyAfterConv.Slice(0, 4));
            if (_authTag != 0 && tag != _authTag) return;

            uint seq = BinaryPrimitives.ReadUInt32LittleEndian(bodyAfterConv.Slice(4, 4));
            byte frameType = bodyAfterConv[8];
            ReadOnlySpan<byte> payloadSpan = bodyAfterConv.Slice(9);
            if (payloadSpan.Length > TransportProtocol.SafeUnreliablePayloadBytes) return;

            Volatile.Write(ref _lastReceiveTick, Environment.TickCount64);
            lock (_kcpLock)
            {
                if (Volatile.Read(ref _closed) != 0) return;
                if (!Equals(_remoteEndPoint, remoteEndPoint))
                    _remoteEndPoint = remoteEndPoint;

                // seq > 0 时启用单调防重放/乱序过滤；seq == 0 表示交由上层按对象序列号过滤。
                if (seq != 0)
                {
                    if (_hasUnreliableInSeq && unchecked((int)(seq - _unreliableInMaxSeq)) <= 0)
                        return;
                    _unreliableInMaxSeq = seq;
                    _hasUnreliableInSeq = true;
                }
            }

            if (frameType != TransportProtocol.FrameBinary && frameType != TransportProtocol.FrameText)
                return;

            byte[] payload = payloadSpan.Length > 0 ? payloadSpan.ToArray() : Array.Empty<byte>();
            var msgType = frameType == TransportProtocol.FrameText
                ? WebSocketMessageType.Text
                : WebSocketMessageType.Binary;
            // 接收通道满时直接丢弃非可靠帧，绝不阻塞 UDP 接收循环。
            _incoming.Writer.TryWrite(new TransportReceiveResult(msgType, payload, payload.Length));
        }

        public void Tick(uint nowMs, long tick64)
        {
            if (Volatile.Read(ref _closed) != 0) return;

            if (tick64 - Volatile.Read(ref _lastReceiveTick) > IdleTimeoutMs)
            {
                CloseInternal(WebSocketCloseStatus.EndpointUnavailable, "KCP idle timeout", sendUdpClose: true);
                return;
            }

            // 未到下一次需要刷新的时间点时直接跳过加锁与 Update。
            uint scheduled = Volatile.Read(ref _nextUpdateMs);
            if (unchecked((int)(nowMs - scheduled)) < 0)
                return;

            bool dead;
            lock (_kcpLock)
            {
                if (Volatile.Read(ref _closed) != 0) return;
                _kcp.Update(nowMs);
                DrainReceivedMessagesLocked();
                Volatile.Write(ref _nextUpdateMs, _kcp.Check(nowMs));
                dead = _kcp.IsDead;
            }

            if (dead)
                CloseInternal(WebSocketCloseStatus.EndpointUnavailable, "KCP dead link", sendUdpClose: false);
        }

        public async Task<TransportReceiveResult> ReceiveMessageAsync(CancellationToken token)
        {
            try
            {
                if (await _incoming.Reader.WaitToReadAsync(token) &&
                    _incoming.Reader.TryRead(out var item))
                {
                    return item;
                }
            }
            catch (ChannelClosedException)
            {
            }

            return TransportReceiveResult.Closed(WebSocketCloseStatus.NormalClosure, "KCP closed");
        }

        public Task SendAsync(byte[] data, WebSocketMessageType type, CancellationToken token)
        {
            if (Volatile.Read(ref _closed) != 0)
                throw new InvalidOperationException("KCP transport is closed.");
            int frameLen = 1 + data.Length;
            if (frameLen > MaxKcpMessageBytes)
                throw new InvalidOperationException($"KCP payload too large: {data.Length}");

            byte frameType = type == WebSocketMessageType.Text
                ? TransportProtocol.FrameText
                : TransportProtocol.FrameBinary;

            byte[] rented = ArrayPool<byte>.Shared.Rent(frameLen);
            try
            {
                rented[0] = frameType;
                if (data.Length > 0)
                    Buffer.BlockCopy(data, 0, rented, 1, data.Length);

                lock (_kcpLock)
                {
                    if (Volatile.Read(ref _closed) != 0)
                        throw new InvalidOperationException("KCP transport is closed.");
                    if (_kcp.Send(rented.AsSpan(0, frameLen)) < 0)
                        throw new InvalidOperationException("KCP send failed.");
                    _kcp.Flush();
                    Volatile.Write(ref _nextUpdateMs, _kcp.Check(CurrentClock()));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }

            return Task.CompletedTask;
        }

        public ValueTask SendUnreliableAsync(byte[] data, WebSocketMessageType type, CancellationToken token)
        {
            if (Volatile.Read(ref _closed) != 0)
                return ValueTask.FromException(new InvalidOperationException("KCP transport is closed."));

            if (type != WebSocketMessageType.Binary || data.Length > TransportProtocol.SafeUnreliablePayloadBytes)
                return new ValueTask(SendAsync(data, type, token));

            int total = TransportProtocol.UnreliableHeaderBytes + data.Length;
            byte[] rented = ArrayPool<byte>.Shared.Rent(total);
            try
            {
                // 服务端下行复用同一 UDP 通道转发多对象/多玩家实时包，seq 置 0 交由上层按 ObjectId 独立去重，
                // 避免公网轻微乱序导致不同对象或语音帧在传输层互相误杀。
                uint seq = 0;
                _ = _unreliableOutSeq;

                Span<byte> span = rented.AsSpan(0, total);
                span[0] = TransportProtocol.UdpUnreliableData;
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), Conv);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(5, 4), _authTag);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(9, 4), seq);
                span[13] = TransportProtocol.FrameBinary;
                if (data.Length > 0)
                    data.AsSpan().CopyTo(span.Slice(TransportProtocol.UnreliableHeaderBytes));

                IPEndPoint endpoint = Volatile.Read(ref _remoteEndPoint);
                _udpSend(endpoint, rented, total);
                return ValueTask.CompletedTask;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken token)
        {
            CloseInternal(closeStatus, statusDescription, sendUdpClose: true);
            return Task.CompletedTask;
        }

        public void Abort()
        {
            CloseInternal(WebSocketCloseStatus.EndpointUnavailable, "Aborted", sendUdpClose: true);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            CloseInternal(WebSocketCloseStatus.NormalClosure, "Disposed", sendUdpClose: false);
            lock (_kcpLock)
            {
                _kcp.Dispose();
            }
        }

        private void CloseInternal(WebSocketCloseStatus status, string reason, bool sendUdpClose)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;

            if (sendUdpClose)
            {
                try
                {
                    byte[] closePayload = TransportProtocol.BuildClosePayload(status, reason);
                    byte[] kcpFrame = new byte[1 + closePayload.Length];
                    kcpFrame[0] = TransportProtocol.FrameClose;
                    Buffer.BlockCopy(closePayload, 0, kcpFrame, 1, closePayload.Length);
                    lock (_kcpLock)
                    {
                        _kcp.Send(kcpFrame);
                        _kcp.Flush();
                    }

                    byte[] udpClose = new byte[7];
                    udpClose[0] = TransportProtocol.UdpClose;
                    BinaryPrimitives.WriteUInt32LittleEndian(udpClose.AsSpan(1, 4), Conv);
                    BinaryPrimitives.WriteUInt16LittleEndian(udpClose.AsSpan(5, 2), (ushort)status);
                    _udpSend(_remoteEndPoint, udpClose, udpClose.Length);
                }
                catch { }
            }

            _incoming.Writer.TryWrite(TransportReceiveResult.Closed(status, reason));
            _incoming.Writer.TryComplete();
            _onClosed(Conv);
        }

        private void DrainReceivedMessagesLocked()
        {
            while (true)
            {
                int size = _kcp.PeekSize();
                if (size <= 0) break;
                if (size > MaxKcpMessageBytes)
                {
                    CloseInternal(WebSocketCloseStatus.MessageTooBig, "KCP message too large", sendUdpClose: true);
                    return;
                }

                if (_recvScratch.Length < size)
                    _recvScratch = new byte[Math.Max(size, _recvScratch.Length * 2)];

                int read = _kcp.Recv(_recvScratch);
                if (read <= 0) break;

                byte frameType = _recvScratch[0];
                int payloadLen = read - 1;
                switch (frameType)
                {
                    case TransportProtocol.FrameBinary:
                    case TransportProtocol.FrameText:
                    {
                        byte[] payload = payloadLen > 0 ? new byte[payloadLen] : Array.Empty<byte>();
                        if (payloadLen > 0) Buffer.BlockCopy(_recvScratch, 1, payload, 0, payloadLen);
                        var msgType = frameType == TransportProtocol.FrameText
                            ? WebSocketMessageType.Text
                            : WebSocketMessageType.Binary;
                        _incoming.Writer.TryWrite(new TransportReceiveResult(msgType, payload, payloadLen));
                        break;
                    }
                    case TransportProtocol.FramePing:
                    {
                        _recvScratch[0] = TransportProtocol.FramePong;
                        _kcp.Send(_recvScratch.AsSpan(0, read));
                        break;
                    }
                    case TransportProtocol.FramePong:
                        break;
                    case TransportProtocol.FrameClose:
                    {
                        var (status, reason) = TransportProtocol.ParseClosePayload(_recvScratch.AsSpan(1, payloadLen));
                        CloseInternal(status, reason, sendUdpClose: false);
                        return;
                    }
                }

                // 大包处理完后收缩暂存缓冲，防止偶发大包常驻内存。
                if (_recvScratch.Length > 64 * 1024)
                    _recvScratch = new byte[4096];
            }
        }

        private void OnKcpOutput(byte[] buffer, int length)
        {
            int total = 1 + length;
            if (_udpOutBuffer.Length < total)
                _udpOutBuffer = new byte[total];
            _udpOutBuffer[0] = TransportProtocol.UdpKcpData;
            Buffer.BlockCopy(buffer, 0, _udpOutBuffer, 1, length);
            _udpSend(_remoteEndPoint, _udpOutBuffer, total);
        }

        private static uint CurrentClock() => unchecked((uint)Environment.TickCount64);
    }
}
