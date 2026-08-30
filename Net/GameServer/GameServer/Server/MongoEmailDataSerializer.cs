using GameData;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace WebSocketDemo;

/// <summary>
/// Maps protobuf EmailData to the document shape also used by the operations site.
/// Protobuf map/repeated properties are getter-only, so MongoDB's automatic mapper
/// cannot deserialize ItemDic or Equips from ordinary BSON documents.
/// </summary>
internal sealed class MongoEmailDataSerializer : SerializerBase<EmailData>, IBsonDocumentSerializer
{
    private const string ProtobufField = "_protobuf";

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, EmailData value)
    {
        var itemDic = new BsonDocument();
        foreach (var item in value.ItemDic)
            itemDic[item.Key] = item.Value;

        var equips = new BsonArray();
        foreach (var equip in value.Equips)
            equips.Add(new BsonBinaryData(equip.ToByteArray()));

        var document = new BsonDocument
        {
            ["_id"] = value.Id,
            [ProtobufField] = new BsonBinaryData(value.ToByteArray()),
            [nameof(EmailData.ItemId)] = value.ItemId,
            [nameof(EmailData.ItemCount)] = value.ItemCount,
            [nameof(EmailData.State)] = value.State,
            [nameof(EmailData.UserId)] = value.UserId,
            [nameof(EmailData.EmailTitle)] = value.EmailTitle,
            [nameof(EmailData.EmailInfo)] = value.EmailInfo,
            [nameof(EmailData.ServerId)] = value.ServerId,
            [nameof(EmailData.CreateTime)] = value.CreateTime,
            [nameof(EmailData.ItemDic)] = itemDic,
            [nameof(EmailData.Equips)] = equips
        };

        BsonDocumentSerializer.Instance.Serialize(context, document);
    }

    public override EmailData Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var document = BsonDocumentSerializer.Instance.Deserialize(context);
        var data = document.TryGetValue(ProtobufField, out var protobuf) && protobuf.IsBsonBinaryData
            ? EmailData.Parser.ParseFrom(protobuf.AsBsonBinaryData.Bytes)
            : new EmailData();

        data.Id = GetString(document, "_id", GetString(document, nameof(EmailData.Id), data.Id));
        data.ItemId = GetString(document, nameof(EmailData.ItemId), data.ItemId);
        data.ItemCount = GetInt64(document, nameof(EmailData.ItemCount), data.ItemCount);
        data.State = GetInt32(document, nameof(EmailData.State), data.State);
        data.UserId = GetString(document, nameof(EmailData.UserId), data.UserId);
        data.EmailTitle = GetString(document, nameof(EmailData.EmailTitle), data.EmailTitle);
        data.EmailInfo = GetString(document, nameof(EmailData.EmailInfo), data.EmailInfo);
        data.ServerId = GetString(document, nameof(EmailData.ServerId), data.ServerId);
        data.CreateTime = GetInt64(document, nameof(EmailData.CreateTime), data.CreateTime);

        if (document.TryGetValue(nameof(EmailData.ItemDic), out var items) && items.IsBsonDocument)
        {
            data.ItemDic.Clear();
            foreach (var item in items.AsBsonDocument)
                if (item.Value.IsNumeric)
                    data.ItemDic[item.Name] = item.Value.ToInt64();
        }

        if (document.TryGetValue(nameof(EmailData.Equips), out var equips) && equips.IsBsonArray)
        {
            data.Equips.Clear();
            foreach (var equip in equips.AsBsonArray)
                if (equip.IsBsonBinaryData)
                    data.Equips.Add(EquipData.Parser.ParseFrom(equip.AsBsonBinaryData.Bytes));
        }

        return data;
    }

    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
    {
        serializationInfo = memberName switch
        {
            nameof(EmailData.Id) => new BsonSerializationInfo("_id", StringSerializer.Instance, typeof(string)),
            nameof(EmailData.ItemCount) or nameof(EmailData.CreateTime) =>
                new BsonSerializationInfo(memberName, Int64Serializer.Instance, typeof(long)),
            nameof(EmailData.State) => new BsonSerializationInfo(memberName, Int32Serializer.Instance, typeof(int)),
            nameof(EmailData.ItemId) or nameof(EmailData.UserId) or nameof(EmailData.EmailTitle) or
            nameof(EmailData.EmailInfo) or nameof(EmailData.ServerId) =>
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
