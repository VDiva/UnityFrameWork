namespace WebSocketDemo;

/// <summary>
/// 统一生成分服数据标识，避免 MongoDB 文档、Redis Key 和在线会话使用不同规则。
/// </summary>
public static class ServerScope
{
    public const string DefaultServerId = "default";

    public static string Normalize(string? serverId)
    {
        var value = serverId?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return DefaultServerId;
        if (value.Length > 64 || value.Contains(':'))
            throw new ArgumentException("serverId is invalid.", nameof(serverId));
        return value;
    }

    public static string UserKey(string serverId, string userId)
    {
        return $"{Normalize(serverId)}:{userId}";
    }

    public static bool TryParseUserKey(string value, out string serverId, out string userId)
    {
        var separator = value.IndexOf(':');
        serverId = separator > 0 ? value[..separator] : string.Empty;
        userId = separator > 0 ? value[(separator + 1)..] : string.Empty;
        return separator > 0 && !string.IsNullOrWhiteSpace(userId);
    }
}
