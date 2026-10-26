using GameData;
using Google.Protobuf;
using WebSocketDemo;

static UserData Inventory()
{
    var data = new UserData();
    data.EquipData.Add(new EquipData { Id = "stored-a", Data = ByteString.CopyFromUtf8("{\"id\":\"legacy-a\"}") });
    data.EquipData.Add(new EquipData { Id = "stored-b", Data = ByteString.CopyFromUtf8("{\"id\":\"legacy-b\"}") });
    data.EquipData.Add(new EquipData { Id = "stored-c", Data = ByteString.CopyFromUtf8("{\"id\":\"legacy-c\"}") });
    return data;
}

static void RejectUnchanged(UserData data, string[] ids)
{
    var original = data.ToByteArray();
    bool rejected = false;
    try { EquipmentDeletion.Apply(data, ids); }
    catch (ArgumentException) { rejected = true; }
    catch (InvalidOperationException) { rejected = true; }
    if (!rejected || !original.SequenceEqual(data.ToByteArray()))
        throw new Exception("Invalid batch must be rejected without partial deletion.");
}

var inventory = Inventory();
inventory.DataDic["Equip_HEAD"] = "legacy-c";
EquipmentDeletion.Apply(inventory, new[] { "stored-a", "stored-b", "stored-a" });
if (inventory.EquipData.Count != 1 || inventory.EquipData[0].Id != "stored-c" ||
    inventory.DataDic["Equip_HEAD"] != "legacy-c")
    throw new Exception("Batch deletion removed the wrong equipment or changed a slot.");

RejectUnchanged(Inventory(), new[] { "stored-a", "another-player-equipment" });
RejectUnchanged(Inventory(), new[] { "legacy-a" });
RejectUnchanged(Inventory(), Array.Empty<string>());
RejectUnchanged(Inventory(), new[] { " " });
RejectUnchanged(Inventory(), new[] { new string('x', 129) });
RejectUnchanged(Inventory(), Enumerable.Repeat("stored-a", 101).ToArray());
foreach (string wornId in new[] { "stored-b", "legacy-b" })
{
    var worn = Inventory();
    worn.DataDic["Equip_BODY"] = wornId;
    RejectUnchanged(worn, new[] { "stored-a", "stored-b" });
}
RejectUnchanged(inventory, new[] { "stored-a" });

var request = new Msg { GameMsgType = GameMsgType.DelEquip };
request.EquipIds.AddRange(new[] { "stored-a", "stored-b" });
var decoded = Msg.Parser.ParseFrom(request.ToByteArray());
if (!decoded.EquipIds.SequenceEqual(request.EquipIds))
    throw new Exception("Delete request IDs failed protobuf round trip.");
Console.WriteLine("Equipment deletion checks passed: batch, ownership, equipped IDs, atomic rejection, duplicate/stale requests, bounds, protobuf.");
