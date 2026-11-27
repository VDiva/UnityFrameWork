using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketDemo.Transport
{
    /// <summary>
    /// 服务端 TCP 传输层：4 字节小端长度前缀 + 1 字节帧类型 + 载荷。
    /// 性能优化：64KB 套接字缓冲、16KB 读缓冲批解析（单次系统调用解析多帧）、复用收发缓冲避免高频 GC。
    /// </summary>
    public sealed class TcpClientTransport : IClientTransport
    {
        private const int SocketBufferBytes = 64 * 1024;
        private const int ReadRingBytes = 16 * 1024;
        private const int MaxReusableBufferBytes = 64 * 1024;

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        // 仅由单接收循环访问：批量从内核读取并复用于消息解码（PlayerSession 同步消费）。
        private readonly byte[] _readRing;
        private int _readPos;
        private int _readLen;
        private byte[] _recvPayload = new byte[4096];

        // 仅在持有 _writeLock 时访问，避免常规小包走 ArrayPool 同步开销。
        private byte[] _writeBuf = new byte[4096];

        private int _closed;
        private int _disposed;

        public string TransportName => "TCP";
        public bool IsConnected => Volatile.Read(ref _closed) == 0 && _client.Connected;

        public TcpClientTransport(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _client.NoDelay = true;
            try
            {
                _client.ReceiveBufferSize = SocketBufferBytes;
                _client.SendBufferSize = SocketBufferBytes;
            }
            catch { }
            _stream = _client.GetStream();
            _readRing = ArrayPool<byte>.Shared.Rent(ReadRingBytes);
        }

        public async Task<TransportReceiveResult> ReceiveMessageAsync(CancellationToken token)
        {
            while (true)
            {
                if (!await EnsureBufferedAsync(5, token))
                {
                    Volatile.Write(ref _closed, 1);
                    return TransportReceiveResult.Closed(WebSocketCloseStatus.NormalClosure, "EOF");
                }

                int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(_readRing.AsSpan(_readPos, 4));
                byte frameType = _readRing[_readPos + 4];
                _readPos += 5;

                if (payloadLength < 0 || payloadLength > TransportProtocol.MaxFrameBytes)
                    throw new InvalidDataException($"Invalid TCP frame length: {payloadLength}");

                byte[] payload;
                if (payloadLength == 0)
                {
                    payload = Array.Empty<byte>();
                }
                else if (payloadLength <= MaxReusableBufferBytes)
                {
                    if (_recvPayload.Length < payloadLength)
                        _recvPayload = new byte[Math.Min(MaxReusableBufferBytes, Math.Max(payloadLength, _recvPayload.Length * 2))];
                    if (!await ReadExactBufferedAsync(_recvPayload, payloadLength, token))
                    {
                        Volatile.Write(ref _closed, 1);
                        return TransportReceiveResult.Closed(WebSocketCloseStatus.NormalClosure, "EOF");
                    }
                    payload = _recvPayload;
                }
                else
                {
                    payload = new byte[payloadLength];
                    if (!await ReadExactBufferedAsync(payload, payloadLength, token))
                    {
                        Volatile.Write(ref _closed, 1);
                        return TransportReceiveResult.Closed(WebSocketCloseStatus.NormalClosure, "EOF");
                    }
                }

                switch (frameType)
                {
                    case TransportProtocol.FrameBinary:
                        return new TransportReceiveResult(WebSocketMessageType.Binary, payload, payloadLength);
                    case TransportProtocol.FrameText:
                        return new TransportReceiveResult(WebSocketMessageType.Text, payload, payloadLength);
                    case TransportProtocol.FramePing:
                    {
                        // 异步回复 Pong 需要独立副本，避免与下一次接收复用 _recvPayload 冲突。
                        byte[] pongCopy = payloadLength > 0 ? payload.AsSpan(0, payloadLength).ToArray() : Array.Empty<byte>();
                        _ = SendFrameSafeAsync(TransportProtocol.FramePong, pongCopy, token);
                        continue;
                    }
                    case TransportProtocol.FramePong:
                        continue;
                    case TransportProtocol.FrameClose:
                        Volatile.Write(ref _closed, 1);
                        var (status, reason) = TransportProtocol.ParseClosePayload(payload.AsSpan(0, payloadLength));
                        return TransportReceiveResult.Closed(status, reason);
                    default:
                        continue;
                }
            }
        }

        public Task SendAsync(byte[] data, WebSocketMessageType type, CancellationToken token)
        {
            byte frameType = type == WebSocketMessageType.Text
                ? TransportProtocol.FrameText
                : TransportProtocol.FrameBinary;
            return SendFrameAsync(frameType, data, token);
        }

        public Task SendBindInfoAsync(uint conv, ReadOnlySpan<byte> bindToken, CancellationToken token)
        {
            byte[] payload = TransportProtocol.BuildBindInfoFrame(conv, bindToken);
            return SendFrameAsync(TransportProtocol.FrameBindInfo, payload, token);
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken token)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try
            {
                byte[] closePayload = TransportProtocol.BuildClosePayload(closeStatus, statusDescription);
                await SendFrameAsync(TransportProtocol.FrameClose, closePayload, token);
            }
            catch { }
            try { _client.Client.Shutdown(SocketShutdown.Send); } catch { }
            try { _client.Close(); } catch { }
        }

        public void Abort()
        {
            Volatile.Write(ref _closed, 1);
            try { _client.Close(); } catch { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Volatile.Write(ref _closed, 1);
            try { _stream.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
            ArrayPool<byte>.Shared.Return(_readRing);
            _writeLock.Dispose();
        }

        private async Task SendFrameSafeAsync(byte frameType, byte[] payload, CancellationToken token)
        {
            try { await SendFrameAsync(frameType, payload, token); }
            catch { }
        }

        private async Task SendFrameAsync(byte frameType, byte[] payload, CancellationToken token)
        {
            int totalLength = 5 + payload.Length;
            await _writeLock.WaitAsync(token);
            try
            {
                if (Volatile.Read(ref _disposed) != 0) throw new IOException("TCP transport disposed.");

                if (totalLength <= MaxReusableBufferBytes)
                {
                    if (_writeBuf.Length < totalLength)
                        _writeBuf = new byte[Math.Min(MaxReusableBufferBytes, Math.Max(totalLength, _writeBuf.Length * 2))];
                    BinaryPrimitives.WriteInt32LittleEndian(_writeBuf.AsSpan(0, 4), payload.Length);
                    _writeBuf[4] = frameType;
                    if (payload.Length > 0)
                        Buffer.BlockCopy(payload, 0, _writeBuf, 5, payload.Length);
                    await _stream.WriteAsync(_writeBuf.AsMemory(0, totalLength), token);
                }
                else
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent(totalLength);
                    try
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(rented.AsSpan(0, 4), payload.Length);
                        rented[4] = frameType;
                        if (payload.Length > 0)
                            Buffer.BlockCopy(payload, 0, rented, 5, payload.Length);
                        await _stream.WriteAsync(rented.AsMemory(0, totalLength), token);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async ValueTask<bool> EnsureBufferedAsync(int required, CancellationToken token)
        {
            while (_readLen - _readPos < required)
            {
                int remaining = _readLen - _readPos;
                if (remaining > 0 && _readPos > 0)
                    Buffer.BlockCopy(_readRing, _readPos, _readRing, 0, remaining);
                _readPos = 0;
                _readLen = remaining;

                int read = await _stream.ReadAsync(_readRing.AsMemory(_readLen, _readRing.Length - _readLen), token);
                if (read <= 0) return false;
                _readLen += read;
            }
            return true;
        }

        private async ValueTask<bool> ReadExactBufferedAsync(byte[] destination, int count, CancellationToken token)
        {
            int offset = 0;
            int buffered = _readLen - _readPos;
            if (buffered > 0)
            {
                int take = Math.Min(buffered, count);
                Buffer.BlockCopy(_readRing, _readPos, destination, 0, take);
                _readPos += take;
                offset += take;
                if (_readPos == _readLen)
                {
                    _readPos = 0;
                    _readLen = 0;
                }
            }

            while (offset < count)
            {
                // 剩余大于半个读缓冲时直接读入目标缓冲，避免二次拷贝。
                if (count - offset >= ReadRingBytes / 2)
                {
                    int read = await _stream.ReadAsync(destination.AsMemory(offset, count - offset), token);
                    if (read <= 0) return false;
                    offset += read;
                }
                else
                {
                    int read = await _stream.ReadAsync(_readRing.AsMemory(0, _readRing.Length), token);
                    if (read <= 0) return false;
                    _readPos = 0;
                    _readLen = read;
                    int take = Math.Min(read, count - offset);
                    Buffer.BlockCopy(_readRing, 0, destination, offset, take);
                    _readPos = take;
                    offset += take;
                }
            }
            return true;
        }
    }
}
