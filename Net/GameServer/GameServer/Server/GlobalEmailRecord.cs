using MongoDB.Bson.Serialization.Attributes;

namespace WebSocketDemo;

public sealed class GlobalEmailRecord
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string Scope { get; set; } = "all";
    public string ServerId { get; set; } = string.Empty;
    public string EmailTitle { get; set; } = string.Empty;
    public string EmailInfo { get; set; } = string.Empty;
    public long CreateTime { get; set; }
    public long StartTime { get; set; }
    public long ExpireTime { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, long> ItemDic { get; set; } = new();
}

public sealed class GlobalEmailReceipt
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string EmailId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public int State { get; set; }
    public long ClaimTime { get; set; }
}
