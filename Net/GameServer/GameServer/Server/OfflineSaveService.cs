namespace WebSocketDemo;

/// <summary>
/// 后台处理玩家下线保存。
/// 这样 Redis/MongoDB 短暂变慢时，不会拖住 WebSocket 主动断开和重连。
/// </summary>
public class OfflineSaveService : BackgroundService
{
    private static readonly TimeSpan SaveTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReconnectGraceTime = TimeSpan.FromSeconds(2);
    private const int MaxConcurrentSaves = 16;
    private readonly SemaphoreSlim _concurrency = new(MaxConcurrentSaves, MaxConcurrentSaves);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var active = new List<Task>();
        try
        {
        await foreach (var user in OfflineSaveQueue.ReadAllAsync(stoppingToken))
        {
            await _concurrency.WaitAsync(stoppingToken);
            var scopeKey = ServerScope.UserKey(user.ServerId, user.UserId);
            active.RemoveAll(task => task.IsCompleted);
            active.Add(SaveOfflineUser(user, scopeKey, stoppingToken));
        }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { await Task.WhenAll(active); }
    }

    private async Task SaveOfflineUser(OfflineUser user, string scopeKey, CancellationToken stoppingToken)
    {
        var acquired = false;
        try
        {
            acquired = true;
            await Task.Delay(ReconnectGraceTime, stoppingToken);
            using var timeoutCts = new CancellationTokenSource(SaveTimeout);
            var saveTask = GameDataMrg.SetPlayerOffline(user.UserId, user.ServerId);
            var completedTask = await Task.WhenAny(saveTask, Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token));

            if (completedTask == saveTask)
            {
                await saveTask;
            }
            else
            {
                Console.WriteLine($"Offline user save timeout: {scopeKey}");
                // 超时不代表底层保存已经停止；保留名额并观察最终结果，防止真实并发失控。
                await saveTask;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Offline user save failed: {scopeKey}, {ex.Message}");
        }
        finally
        {
            if (acquired)
            {
                _concurrency.Release();
            }
        }
    }
}
