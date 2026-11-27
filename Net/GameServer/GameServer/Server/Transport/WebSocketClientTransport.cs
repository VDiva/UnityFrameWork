using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketDemo.Transport
{
    public sealed class WebSocketClientTransport : IClientTransport
    {
        private const int ReceiveBufferSize = 4096;
        private readonly WebSocket _webSocket;
        private readonly byte[] _receiveBuffer;
        private readonly MemoryStream _messageBuffer = new(ReceiveBufferSize);
        private int _disposed;

        public string TransportName => "WebSocket";
        public bool IsConnected => _webSocket.State == WebSocketState.Open;

        public WebSocketClientTransport(WebSocket webSocket)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _receiveBuffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
        }

        public async Task<TransportReceiveResult> ReceiveMessageAsync(CancellationToken token)
        {
            _messageBuffer.SetLength(0);
            WebSocketReceiveResult result;
            bool singleFrame = false;

            do
            {
                result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(_receiveBuffer, 0, ReceiveBufferSize),
                    token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return TransportReceiveResult.Closed(
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        result.CloseStatusDescription);
                }

                if (_messageBuffer.Length + result.Count > TransportProtocol.MaxFrameBytes)
                {
                    throw new InvalidDataException("WebSocket message exceeds maximum allowed size.");
                }

                if (result.EndOfMessage && _messageBuffer.Length == 0)
                    singleFrame = true;
                else
                    _messageBuffer.Write(_receiveBuffer, 0, result.Count);
            } while (!result.EndOfMessage);

            int length = singleFrame ? result.Count : checked((int)_messageBuffer.Length);
            byte[] payload = singleFrame ? _receiveBuffer : _messageBuffer.GetBuffer();
            // 复制为独立数组或直接返回给单接收循环同步消费；若大容量则即时收缩。
            if (!singleFrame && _messageBuffer.Capacity > 64 * 1024)
            {
                byte[] copy = new byte[length];
                Buffer.BlockCopy(payload, 0, copy, 0, length);
                _messageBuffer.SetLength(0);
                _messageBuffer.Capacity = ReceiveBufferSize;
                return new TransportReceiveResult(result.MessageType, copy, length);
            }

            return new TransportReceiveResult(result.MessageType, payload, length);
        }

        public Task SendAsync(byte[] data, WebSocketMessageType type, CancellationToken token)
        {
            return _webSocket.SendAsync(new ArraySegment<byte>(data), type, true, token);
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken token)
        {
            if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
            {
                await _webSocket.CloseAsync(closeStatus, statusDescription, token);
            }
        }

        public void Abort()
        {
            try { _webSocket.Abort(); } catch { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { _webSocket.Dispose(); } catch { }
            _messageBuffer.Dispose();
            ArrayPool<byte>.Shared.Return(_receiveBuffer);
        }
    }
}
