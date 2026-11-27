using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketDemo.Transport
{
    /// <summary>
    /// 监听原生客户端 KCP（UDP）连接：
    /// 1) 独立 KCP 模式（UdpConnectReq -> 创建 PlayerSession）；
    /// 2) TCP+KCP 双通道模式（UdpBindReq -> 校验绑定令牌并附着到已有 TCP PlayerSession）。
    /// 性能优化：4MB 内核 UDP 缓冲区、复用的零分配接收缓冲与零拷贝 Socket.SendTo。
    /// </summary>
    public sealed class KcpServerService : BackgroundService
    {
        private const int UdpSocketBufferBytes = 4 * 1024 * 1024;
        private readonly int _port;
        private UdpClient? _udp;
        private readonly ConcurrentDictionary<uint, KcpClientTransport> _transports = new();
        private readonly ConcurrentDictionary<(string EndPoint, uint Nonce), (uint Conv, long CreatedAt)> _recentHandshakes = new();

        public int ListeningPort => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port ?? _port;

        public KcpServerService(IConfiguration configuration)
        {
            var value = configuration["Server:KcpPort"] ?? Environment.GetEnvironmentVariable("GAME_SERVER_KCP_PORT");
            _port = int.TryParse(value, out int parsed) ? parsed : 5102;
        }

        public KcpServerService(int port)
        {
            _port = port;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_port < 0)
            {
                Console.WriteLine("KCP server disabled (Server:KcpPort < 0).");
                return;
            }

            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            ConfigureUdpSocket(_udp);
            Console.WriteLine($"KCP server listening on UDP port {ListeningPort}.");

            byte[] receiveBuffer = new byte[65536];
            EndPoint anyEndPoint = new IPEndPoint(IPAddress.Any, 0);
            var tickTask = RunKcpTickLoopAsync(stoppingToken);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    SocketReceiveFromResult received;
                    try
                    {
                        received = await _udp.Client.ReceiveFromAsync(
                            receiveBuffer.AsMemory(), SocketFlags.None, anyEndPoint, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"KCP UDP receive error: {ex.SocketErrorCode}");
                        continue;
                    }

                    if (received.ReceivedBytes > 0 && received.RemoteEndPoint is IPEndPoint remoteEndPoint)
                        HandleDatagram(receiveBuffer.AsSpan(0, received.ReceivedBytes), remoteEndPoint, stoppingToken);
                }
            }
            finally
            {
                foreach (var transport in _transports.Values)
                    transport.Abort();
                _transports.Clear();
                try { _udp.Dispose(); } catch { }
                try { await tickTask; } catch { }
            }
        }

        private void HandleDatagram(ReadOnlySpan<byte> buffer, IPEndPoint remoteEndPoint, CancellationToken stoppingToken)
        {
            if (buffer.Length < 1) return;
            byte kind = buffer[0];

            switch (kind)
            {
                case TransportProtocol.UdpKcpData:
                    // 热路径放最前面，减少高频数据包分支判断。
                    if (buffer.Length >= 1 + KcpCore.Overhead)
                    {
                        var segment = buffer.Slice(1);
                        uint conv = KcpCore.ReadConv(segment);
                        if (_transports.TryGetValue(conv, out var transport))
                            transport.OnUdpData(segment, remoteEndPoint);
                    }
                    break;
                case TransportProtocol.UdpUnreliableData:
                    if (buffer.Length >= TransportProtocol.UnreliableHeaderBytes)
                    {
                        uint conv = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
                        if (_transports.TryGetValue(conv, out var transport))
                            transport.OnUdpUnreliableData(buffer.Slice(5), remoteEndPoint);
                    }
                    break;
                case TransportProtocol.UdpConnectReq:
                    HandleStandaloneConnect(buffer, remoteEndPoint, stoppingToken);
                    break;
                case TransportProtocol.UdpBindReq:
                    HandleTcpBindRequest(buffer, remoteEndPoint);
                    break;
                case TransportProtocol.UdpClose:
                    if (buffer.Length >= 7)
                    {
                        uint conv = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
                        var status = (WebSocketCloseStatus)BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(5, 2));
                        if (_transports.TryGetValue(conv, out var transport))
                            transport.OnRemoteUdpClose(status);
                    }
                    break;
            }
        }

        private void HandleStandaloneConnect(ReadOnlySpan<byte> buffer, IPEndPoint remoteEndPoint, CancellationToken stoppingToken)
        {
            if (buffer.Length < 5) return;
            uint nonce = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
            var handshakeKey = (remoteEndPoint.ToString(), nonce);

            if (_recentHandshakes.TryGetValue(handshakeKey, out var existing) &&
                _transports.ContainsKey(existing.Conv))
            {
                SendConnectAck(remoteEndPoint, nonce, existing.Conv);
                return;
            }

            if (!PlayerSessionManager.Instance.CanAcceptSession) return;

            uint conv = KcpBindRegistry.AllocateConv();
            var transport = new KcpClientTransport(conv, remoteEndPoint, SendUdp, OnTransportClosed, authTag: nonce);
            if (!_transports.TryAdd(conv, transport))
            {
                transport.Dispose();
                return;
            }

            _recentHandshakes[handshakeKey] = (conv, Environment.TickCount64);
            SendConnectAck(remoteEndPoint, nonce, conv);

            _ = RunStandaloneSessionAsync(transport, stoppingToken);
        }

        private void HandleTcpBindRequest(ReadOnlySpan<byte> buffer, IPEndPoint remoteEndPoint)
        {
            if (buffer.Length < 5 + TransportProtocol.BindTokenBytes) return;
            uint conv = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(1, 4));
            ReadOnlySpan<byte> token = buffer.Slice(5, TransportProtocol.BindTokenBytes);

            if (!KcpBindRegistry.TryValidateAndGetSession(conv, token, out PlayerSession? session) || session == null)
            {
                SendBindResponse(remoteEndPoint, TransportProtocol.UdpBindReject, conv);
                return;
            }

            if (_transports.ContainsKey(conv))
            {
                SendBindResponse(remoteEndPoint, TransportProtocol.UdpBindAck, conv);
                return;
            }

            uint authTag = BinaryPrimitives.ReadUInt32LittleEndian(token.Slice(0, 4));
            var kcpTransport = new KcpClientTransport(conv, remoteEndPoint, SendUdp, OnTransportClosed, authTag);
            if (!_transports.TryAdd(conv, kcpTransport))
            {
                kcpTransport.Dispose();
                SendBindResponse(remoteEndPoint, TransportProtocol.UdpBindAck, conv);
                return;
            }

            session.AttachRealtimeTransport(kcpTransport);
            SendBindResponse(remoteEndPoint, TransportProtocol.UdpBindAck, conv);
        }

        private static async Task RunStandaloneSessionAsync(KcpClientTransport transport, CancellationToken stoppingToken)
        {
            string playerId = Guid.NewGuid().ToString();
            var session = new PlayerSession(playerId, transport, stoppingToken);
            if (!PlayerSessionManager.Instance.TryAddSession(playerId, session))
            {
                await session.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server is busy");
                session.Dispose();
                return;
            }

            try
            {
                await session.WaitForCloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"KCP session failed: {playerId}, {ex.GetType().Name}, {ex.Message}");
            }
            finally
            {
                await PlayerSessionManager.Instance.RemoveSessionAsync(playerId);
            }
        }

        private async Task RunKcpTickLoopAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
            long lastCleanup = Environment.TickCount64;
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    long tick64 = Environment.TickCount64;
                    uint nowMs = unchecked((uint)tick64);
                    foreach (var pair in _transports)
                    {
                        pair.Value.Tick(nowMs, tick64);
                    }

                    if (tick64 - lastCleanup > 10_000)
                    {
                        lastCleanup = tick64;
                        foreach (var pair in _recentHandshakes)
                        {
                            if (tick64 - pair.Value.CreatedAt > 15_000)
                                _recentHandshakes.TryRemove(pair.Key, out _);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnTransportClosed(uint conv)
        {
            _transports.TryRemove(conv, out _);
        }

        private void SendConnectAck(IPEndPoint remoteEndPoint, uint nonce, uint conv)
        {
            Span<byte> ack = stackalloc byte[9];
            ack[0] = TransportProtocol.UdpConnectAck;
            BinaryPrimitives.WriteUInt32LittleEndian(ack.Slice(1, 4), nonce);
            BinaryPrimitives.WriteUInt32LittleEndian(ack.Slice(5, 4), conv);
            SendUdpSpan(remoteEndPoint, ack);
        }

        private void SendBindResponse(IPEndPoint remoteEndPoint, byte kind, uint conv)
        {
            Span<byte> ack = stackalloc byte[5];
            ack[0] = kind;
            BinaryPrimitives.WriteUInt32LittleEndian(ack.Slice(1, 4), conv);
            SendUdpSpan(remoteEndPoint, ack);
        }

        private void SendUdp(IPEndPoint remoteEndPoint, byte[] data, int length)
        {
            SendUdpSpan(remoteEndPoint, data.AsSpan(0, length));
        }

        private void SendUdpSpan(IPEndPoint remoteEndPoint, ReadOnlySpan<byte> data)
        {
            try
            {
                _udp?.Client.SendTo(data, SocketFlags.None, remoteEndPoint);
            }
            catch
            {
            }
        }

        private static void ConfigureUdpSocket(UdpClient udp)
        {
            try
            {
                udp.Client.ReceiveBufferSize = UdpSocketBufferBytes;
                udp.Client.SendBufferSize = UdpSocketBufferBytes;
            }
            catch { }

            if (!OperatingSystem.IsWindows()) return;
            try
            {
                const int sioUdpConnReset = -1744830452;
                udp.Client.IOControl((IOControlCode)sioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch
            {
            }
        }
    }
}
