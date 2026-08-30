using MongoDB.Bson.Serialization.Attributes;

namespace GameData;

/// <summary>
/// Announcement documents may contain fields written by older operations-site versions.
/// Keep protobuf focused on fields sent to clients while MongoDB safely ignores legacy fields.
/// </summary>
[BsonIgnoreExtraElements]
public sealed partial class GongGaoData
{
}
