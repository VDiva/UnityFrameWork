using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketDemo.Transport
{
    /// <summary>
    /// 监听原生客户端 TCP 连接（非 Web 平台默认可靠主通道），建连后下发 KCP 绑定令牌支持双通道加速。
    /// </summary>
    public sealed class TcpServerService : BackgroundService
    {
        private readonly int _port;
        private TcpListener? _listener;

        public int ListeningPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? _port;

        public TcpServerService(IConfiguration configuration)
        {
            var value = configuration["Server:TcpPort"] ?? Environment.GetEnvironmentVariable("GAME_SERVER_TCP_PORT");
            _port = int.TryParse(value, out int parsed) ? parsed : 5101;
        }

        public TcpServerService(int port)
        {
            _port = port;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_port < 0)
            {
                Console.WriteLine("TCP server disabled (Server:TcpPort < 0).");
                return;
            }

            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"TCP server listening on port {ListeningPort}.");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"TCP accept error: {ex.Message}");
                        await Task.Delay(50, stoppingToken);
                        continue;
                    }

                    _ = HandleClientAsync(client, stoppingToken);
                }
            }
            finally
            {
                try { _listener.Stop(); } catch { }
            }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
        {
            if (!PlayerSessionManager.Instance.CanAcceptSession)
            {
                try { client.Close(); } catch { }
                return;
            }

            string playerId = Guid.NewGuid().ToString();
            var transport = new TcpClientTransport(client);
            var session = new PlayerSession(playerId, transport, stoppingToken);

            if (!PlayerSessionManager.Instance.TryAddSession(playerId, session))
            {
                await session.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server is busy");
                session.Dispose();
                return;
            }

            var (conv, bindToken) = KcpBindRegistry.RegisterSession(session);
            session.KcpBindConv = conv;

            try
            {
                await transport.SendBindInfoAsync(conv, bindToken, stoppingToken);
                await session.WaitForCloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TCP session failed: {playerId}, {ex.GetType().Name}, {ex.Message}");
            }
            finally
            {
                KcpBindRegistry.Unregister(conv);
                await PlayerSessionManager.Instance.RemoveSessionAsync(playerId);
            }
        }
    }
}
