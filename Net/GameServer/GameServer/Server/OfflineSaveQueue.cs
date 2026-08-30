using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace WebSocketDemo;

/// <summary>
/// 玩家下线保存队列。
/// WebSocket 断开时只入队，不等待 MongoDB/Redis 操作完成，避免阻塞连接释放。
/// </summary>
public readonly record struct OfflineUser(string UserId, string ServerId);

public static class OfflineSaveQueue
{
    private static readonly ConcurrentDictionary<OfflineUser, byte> Pending = new();
    private static readonly Channel<OfflineUser> Channel = System.Threading.Channels.Channel.CreateUnbounded<OfflineUser>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public static bool TryEnqueue(string userId, string? serverId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = new OfflineUser(userId, ServerScope.Normalize(serverId));
        if (!Pending.TryAdd(user, 0)) return true;
        if (Channel.Writer.TryWrite(user)) return true;
        Pending.TryRemove(user, out _);
        return false;
    }

    public static async IAsyncEnumerable<OfflineUser> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var user in Channel.Reader.ReadAllAsync(cancellationToken))
        {
            // 已排队的重复离线合并；开始处理后再次离线仍可排队，不丢后续保存。
            Pending.TryRemove(user, out _);
            yield return user;
        }
    }
}
