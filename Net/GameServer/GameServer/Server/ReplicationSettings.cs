using Microsoft.Extensions.Configuration;

namespace WebSocketDemo;

/// <summary>仅控制实时状态发送窗口，不改变房间业务或引入游戏模拟 Tick。</summary>
public static class ReplicationSettings
{
    private static int _realtimeSendRate = 30;
    public static int RealtimeSendRate => Volatile.Read(ref _realtimeSendRate);
    public static TimeSpan RealtimeInterval
    {
        get
        {
            int rate = RealtimeSendRate;
            return rate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(1d / rate);
        }
    }

    public static void Configure(IConfiguration configuration)
    {
        string? value = configuration["Networking:RealtimeSendRate"];
        int rate = value == null ? 30 : int.TryParse(value, out int parsed) ? parsed : -1;
        if (rate != 0 && (rate < 5 || rate > 120))
            throw new InvalidOperationException("Networking:RealtimeSendRate 必须为 0（关闭限频）或 5 到 120。");
        Volatile.Write(ref _realtimeSendRate, rate);
    }
}
