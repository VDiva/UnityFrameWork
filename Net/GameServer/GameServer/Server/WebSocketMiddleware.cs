using System.Net;
using System.Net.WebSockets;

namespace WebSocketDemo
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next(context);
                return;
            }

            if (!PlayerSessionManager.Instance.CanAcceptSession)
            {
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                await context.Response.WriteAsync("Server is busy.");
                return;
            }

            string playerId = Guid.NewGuid().ToString();
            using var acceptTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            acceptTimeout.CancelAfter(TimeSpan.FromSeconds(10));

            WebSocket webSocket;
            try
            {
                webSocket = await context.WebSockets.AcceptWebSocketAsync().WaitAsync(acceptTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Accept websocket timeout: {playerId}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept websocket failed: {ex.Message}");
                return;
            }

            var session = new PlayerSession(playerId, webSocket, context.RequestAborted);
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
                Console.WriteLine($"Websocket session failed: {playerId}, {ex.GetType().Name}, {ex.Message}");
            }
            finally
            {
                await PlayerSessionManager.Instance.RemoveSessionAsync(playerId);
            }
        }
    }
}
