using GameData;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace WebSocketDemo;

/// <summary>
/// Protobuf map/repeated fields are getter-only and are ignored by MongoDB's default class mapper.
/// Directly streams the complete protobuf binary and indexed scalar fields via IBsonWriter/IBsonReader
/// without allocating intermediate BsonDocument DOM trees.
/// </summary>
internal sealed class MongoUserDataSerializer : SerializerBase<UserData>, IBsonDocumentSerializer
{
    private const string ProtobufField = "_protobuf";

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, UserData value)
    {
        var writer = context.Writer;
        writer.WriteStartDocument();

        writer.WriteName(ProtobufField);
        writer.WriteBytes(value.ToByteArray());

        writer.WriteName(nameof(UserData.Name));
        writer.WriteString(value.Name ?? string.Empty);

        writer.WriteName(nameof(UserData.UserId));
        writer.WriteString(value.UserId ?? string.Empty);

        writer.WriteName(nameof(UserData.Server));
        writer.WriteString(value.Server ?? string.Empty);

        writer.WriteName(nameof(UserData.LoginTime));
        writer.WriteInt64(value.LoginTime);

        writer.WriteName(nameof(UserData.FightingCapacity));
        writer.WriteString(value.FightingCapacity ?? string.Empty);

        writer.WriteName(nameof(UserData.ServerId));
        writer.WriteString(value.ServerId ?? string.Empty);

        writer.WriteName(nameof(UserData.RoleType));
        writer.WriteInt32(value.RoleType);

        writer.WriteName(nameof(UserData.CreatedAt));
        writer.WriteInt64(value.CreatedAt);

        writer.WriteName(nameof(UserData.UserAndServerId));
        writer.WriteString(value.UserAndServerId ?? string.Empty);

        writer.WriteName(nameof(UserData.Avatar));
        writer.WriteString(value.Avatar ?? string.Empty);

        writer.WriteEndDocument();
    }

    public override UserData Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        reader.ReadStartDocument();

        byte[]? protoBytes = null;
        string? name = null;
        string? userId = null;
        string? server = null;
        bool hasLoginTime = false;
        long loginTime = 0;
        string? fightingCapacity = null;
        string? serverId = null;
        bool hasRoleType = false;
        int roleType = 0;
        bool hasCreatedAt = false;
        long createdAt = 0;
        string? userAndServerId = null;
        string? avatar = null;

        BsonType bsonType;
        while ((bsonType = reader.ReadBsonType()) != BsonType.EndOfDocument)
        {
            string fieldName = reader.ReadName(Utf8NameDecoder.Instance);
            switch (fieldName)
            {
                case ProtobufField when bsonType == BsonType.Binary:
                    protoBytes = reader.ReadBytes();
                    break;
                case nameof(UserData.Name) when bsonType == BsonType.String:
                    name = reader.ReadString();
                    break;
                case nameof(UserData.UserId) when bsonType == BsonType.String:
                    userId = reader.ReadString();
                    break;
                case nameof(UserData.Server) when bsonType == BsonType.String:
                    server = reader.ReadString();
                    break;
                case nameof(UserData.LoginTime) when IsNumeric(bsonType):
                    loginTime = ReadInt64(reader, bsonType);
                    hasLoginTime = true;
                    break;
                case nameof(UserData.FightingCapacity) when bsonType == BsonType.String:
                    fightingCapacity = reader.ReadString();
                    break;
                case nameof(UserData.ServerId) when bsonType == BsonType.String:
                    serverId = reader.ReadString();
                    break;
                case nameof(UserData.RoleType) when IsNumeric(bsonType):
                    roleType = ReadInt32(reader, bsonType);
                    hasRoleType = true;
                    break;
                case nameof(UserData.CreatedAt) when IsNumeric(bsonType):
                    createdAt = ReadInt64(reader, bsonType);
                    hasCreatedAt = true;
                    break;
                case nameof(UserData.UserAndServerId) when bsonType == BsonType.String:
                    userAndServerId = reader.ReadString();
                    break;
                case nameof(UserData.Avatar) when bsonType == BsonType.String:
                    avatar = reader.ReadString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        reader.ReadEndDocument();

        var data = protoBytes != null ? UserData.Parser.ParseFrom(protoBytes) : new UserData();

        // Partial MongoDB updates change the searchable scalar fields without rewriting _protobuf.
        // Overlay them so the returned protobuf object always reflects the newest database values.
        if (name != null) data.Name = name;
        if (userId != null) data.UserId = userId;
        if (server != null) data.Server = server;
        if (hasLoginTime) data.LoginTime = loginTime;
        if (fightingCapacity != null) data.FightingCapacity = fightingCapacity;
        if (serverId != null) data.ServerId = serverId;
        if (hasRoleType) data.RoleType = roleType;
        if (hasCreatedAt) data.CreatedAt = createdAt;
        if (userAndServerId != null) data.UserAndServerId = userAndServerId;
        if (avatar != null) data.Avatar = avatar;
        return data;
    }

    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
    {
        serializationInfo = memberName switch
        {
            nameof(UserData.LoginTime) => new BsonSerializationInfo(memberName, Int64Serializer.Instance, typeof(long)),
            nameof(UserData.RoleType) => new BsonSerializationInfo(memberName, Int32Serializer.Instance, typeof(int)),
            nameof(UserData.CreatedAt) => new BsonSerializationInfo(memberName, Int64Serializer.Instance, typeof(long)),
            nameof(UserData.Name) or
            nameof(UserData.UserId) or
            nameof(UserData.Server) or
            nameof(UserData.FightingCapacity) or
            nameof(UserData.ServerId) or
            nameof(UserData.UserAndServerId) or
            nameof(UserData.Avatar) =>
                new BsonSerializationInfo(memberName, StringSerializer.Instance, typeof(string)),
            _ => null!
        };
        return serializationInfo != null;
    }

    private static bool IsNumeric(BsonType type) =>
        type is BsonType.Int32 or BsonType.Int64 or BsonType.Double;

    private static long ReadInt64(IBsonReader reader, BsonType type) => type switch
    {
        BsonType.Int64 => reader.ReadInt64(),
        BsonType.Int32 => reader.ReadInt32(),
        BsonType.Double => (long)reader.ReadDouble(),
        _ => 0L
    };

    private static int ReadInt32(IBsonReader reader, BsonType type) => type switch
    {
        BsonType.Int32 => reader.ReadInt32(),
        BsonType.Int64 => (int)reader.ReadInt64(),
        BsonType.Double => (int)reader.ReadDouble(),
        _ => 0
    };
}
