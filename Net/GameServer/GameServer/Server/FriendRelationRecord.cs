using MongoDB.Bson.Serialization.Attributes;

namespace WebSocketDemo;

/// <summary>
/// MongoDB 中的双向好友关系。一对好友只保存一条记录，用户顺序经过标准化处理。
/// </summary>
internal sealed class FriendRelationRecord
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string ServerId { get; set; } = string.Empty;
    public string UserAId { get; set; } = string.Empty;
    public string UserBId { get; set; } = string.Empty;
    public long CreatedAt { get; set; }

    /// <summary>创建具有稳定主键的双向好友关系记录。</summary>
    /// <param name="userId">关系一方的玩家 ID。</param>m
    /// <param name="friendUserId">关系另一方的玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>用户顺序已经标准化、可用于新增或删除的关系记录。</returns>
    public static FriendRelationRecord Create(string userId, string friendUserId, string serverId)
    {
        serverId = ServerScope.Normalize(serverId);
        string first;
        string second;
        if (string.CompareOrdinal(userId, friendUserId) <= 0)
        {
            first = userId;
            second = friendUserId;
        }
        else
        {
            first = friendUserId;
            second = userId;
        }

        return new FriendRelationRecord
        {
            Id = $"{serverId.Length}:{serverId}{first.Length}:{first}{second.Length}:{second}",
            ServerId = serverId,
            UserAId = first,
            UserBId = second,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}
