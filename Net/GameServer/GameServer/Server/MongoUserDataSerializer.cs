using GameData;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace WebSocketDemo;

/// <summary>
/// Protobuf map/repeated fields are getter-only and are ignored by MongoDB's default class mapper.
/// Store the complete protobuf message as binary while keeping frequently queried scalar fields
/// alongside it for indexes and partial updates.
/// </summary>
internal sealed class MongoUserDataSerializer : SerializerBase<UserData>, IBsonDocumentSerializer
{
    private const string ProtobufField = "_protobuf";

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, UserData value)
    {
        var document = new BsonDocument
        {
            [ProtobufField] = new BsonBinaryData(value.ToByteArray()),
            [nameof(UserData.Name)] = value.Name,
            [nameof(UserData.UserId)] = value.UserId,
            [nameof(UserData.Server)] = value.Server,
            [nameof(UserData.LoginTime)] = value.LoginTime,
            [nameof(UserData.FightingCapacity)] = value.FightingCapacity,
            [nameof(UserData.ServerId)] = value.ServerId,
            [nameof(UserData.RoleType)] = value.RoleType,
            [nameof(UserData.CreatedAt)] = value.CreatedAt,
            [nameof(UserData.UserAndServerId)] = value.UserAndServerId,
            [nameof(UserData.Avatar)] = value.Avatar
        };

        BsonDocumentSerializer.Instance.Serialize(context, document);
    }

    public override UserData Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var document = BsonDocumentSerializer.Instance.Deserialize(context);
        var data = document.TryGetValue(ProtobufField, out var protobuf) && protobuf.IsBsonBinaryData
            ? UserData.Parser.ParseFrom(protobuf.AsBsonBinaryData.Bytes)
            : new UserData();

        // Partial MongoDB updates change the searchable scalar fields without rewriting _protobuf.
        // Overlay them so the returned protobuf object always reflects the newest database values.
        data.Name = GetString(document, nameof(UserData.Name), data.Name);
        data.UserId = GetString(document, nameof(UserData.UserId), data.UserId);
        data.Server = GetString(document, nameof(UserData.Server), data.Server);
        data.LoginTime = GetInt64(document, nameof(UserData.LoginTime), data.LoginTime);
        data.FightingCapacity = GetString(document, nameof(UserData.FightingCapacity), data.FightingCapacity);
        data.ServerId = GetString(document, nameof(UserData.ServerId), data.ServerId);
        data.RoleType = GetInt32(document, nameof(UserData.RoleType), data.RoleType);
        data.CreatedAt = GetInt64(document, nameof(UserData.CreatedAt), data.CreatedAt);
        data.UserAndServerId = GetString(document, nameof(UserData.UserAndServerId), data.UserAndServerId);
        data.Avatar = GetString(document, nameof(UserData.Avatar), data.Avatar);
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

    private static string GetString(BsonDocument document, string name, string fallback) =>
        document.TryGetValue(name, out var value) && value.IsString ? value.AsString : fallback;

    private static long GetInt64(BsonDocument document, string name, long fallback) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt64() : fallback;

    private static int GetInt32(BsonDocument document, string name, int fallback) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : fallback;
}
