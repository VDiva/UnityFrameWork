namespace WebSocketDemo;

/// <summary>
/// 定时把 Redis 中标记为 dirty 的玩家数据保存到 MongoDB。
/// 这样即使服务器异常崩溃，也最多损失一个保存周期内的数据。
/// </summary>
public class DirtyUserSaveService : BackgroundService
{
    // 大规模在线时扩大合并窗口，降低 MongoDB 周期写入压力。
    // 玩家断线、主动退出和服务器关闭仍会立即触发强制落库。
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SaveInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await SaveDirtyUsers("timer");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dirty user timer save failed: {ex.Message}");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await SaveDirtyUsers("service stop");
        await base.StopAsync(cancellationToken);
    }

    private static async Task SaveDirtyUsers(string reason)
    {
        if (!RedisMrg.IsConnected || !MongoDBMrg.IsConnected)
        {
            return;
        }

        var savedCount = await GameDataMrg.SaveAllDirtyUsersToMongo();
        if (savedCount > 0)
        {
            Console.WriteLine($"Dirty users saved by {reason}: {savedCount}");
        }
    }
}
