namespace WebSocketDemo;

public class ServerListService:BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GameDataMrg.RefreshGameServerCaches();
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"GetServerList Error: {e.Message}");
            }
        }
    }
}