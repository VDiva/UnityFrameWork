namespace WebSocketDemo;

/// <summary>
/// 定时清理已经不再 Open 的 WebSocket 会话。
/// 反向代理/WSS 下关闭事件偶尔会比本地 ws 慢，这里做一层兜底清理。
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                PlayerSessionManager.Instance.RemoveDisconnectedSessions();
                PlayerSessionManager.Instance.RemoveExpiredSessions();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session cleanup failed: {ex.Message}");
            }
        }
    }
}
