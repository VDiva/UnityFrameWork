namespace WebSocketDemo;

/// <summary>
/// 每周清除排行榜。
/// 触发时间按服务器本地时间计算：周一 00:00，也就是周日结束的午夜。
/// </summary>
public class WeeklyRankClearService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextClearTime = GetNextMondayMidnight(DateTimeOffset.Now);
                var delay = nextClearTime - DateTimeOffset.Now;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                Console.WriteLine($"Next weekly rank clear time: {nextClearTime:yyyy-MM-dd HH:mm:ss zzz}");
                await Task.Delay(delay, stoppingToken);

                var rewardMailCount = await GameDataMrg.AwardWeeklyRankRewards();
                Console.WriteLine($"Weekly rank reward mails created: {rewardMailCount}");

                await GameDataMrg.ClearAllRankData();
                Console.WriteLine("Weekly all rank data cleared.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Weekly rank clear failed: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private static DateTimeOffset GetNextMondayMidnight(DateTimeOffset now)
    {
        var todayMidnight = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        var nextMondayMidnight = todayMidnight.AddDays(daysUntilMonday);

        if (nextMondayMidnight <= now)
        {
            nextMondayMidnight = nextMondayMidnight.AddDays(7);
        }

        return nextMondayMidnight;
    }
}
