namespace WebSocketDemo;

/// <summary>
/// 定时把在线玩家邮件从 MongoDB 刷新到 Redis。
/// 业务查询邮件只读 Redis，降低频繁查 MongoDB 的压力。
/// </summary>
public class EmailCacheRefreshService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var refreshedCount = await GameDataMrg.RefreshOnlineEmailCaches();
                if (refreshedCount > 0)
                {
                    Console.WriteLine($"Email caches refreshed: {refreshedCount}");
                }

            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email cache refresh failed: {ex.Message}");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
