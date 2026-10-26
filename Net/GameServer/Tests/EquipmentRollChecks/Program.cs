using GameData;
using Google.Protobuf;
using WebSocketDemo;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}
var dice = new Queue<int>(new[] { 60, 90, 90, 12, 99 });
var result = EquipmentRoll.Resolve(3, () => dice.Dequeue());
Check(result.WinnerIndex == 2 && result.Rounds.Count == 2 &&
    result.Rounds[1].Select(s => s.PlayerIndex).SequenceEqual(new[] { 1, 2 }),
    "only tied highest players reroll; unique highest wins");
Check(EquipmentRoll.Resolve(1, () => 1).WinnerIndex == 0, "single player receives equipment");
var extremes = new Queue<int>(new[] { 1, 100 });
Check(EquipmentRoll.Resolve(2, () => extremes.Dequeue()).WinnerIndex == 1, "100 beats 1");
var manager = PlayerSessionManager.Instance;
var owner = new PlayerSession("owner", 2);
var guest = new PlayerSession("guest", 2);
var outsider = new PlayerSession("outsider", 3);
var offline = new PlayerSession("offline", 2) { IsConnected = false };
manager.Sessions.AddRange(new[] { owner, guest, outsider, offline });
var objects = NetworkRoomObjectManager.Instance;
async Task<uint> Spawn(int quality)
{
    await objects.SpawnAsync(owner, new NetworkObjectData
    {
        PrefabId = "DiaoLuo", RoleType = 5, EquipmentDrop = new EquipData
        { Id = "untrusted-id", Data = ByteString.CopyFromUtf8($"{{\"equipKey\":\"test\",\"pz\":{quality}}}") }
    }, new NetworkTransformData { PositionX = 12 });
    return manager.Broadcasts.Last(m => m.ServerMsgType == ServerMsgType.NetworkObjectSpawn).NetworkObject.ObjectId;
}
uint drop = await Spawn(3);
var spawn = manager.Broadcasts.Last().NetworkObject;
Check(spawn.EquipmentRollRequired && spawn.EquipmentDrop.Id != "untrusted-id",
    "purple equipment registered for rolling with server generated ID");
Check(NetworkObjectData.Parser.ParseFrom(spawn.ToByteArray()).EquipmentRollRequired,
    "roll requirement survives wire round trip");
await objects.ClaimAsync(guest, drop);
Check(guest.Messages.Last().RoleType == 0, "ordinary pickup cannot claim rolling equipment");
await objects.RollEquipmentAsync(outsider, drop);
await objects.RollEquipmentAsync(guest, drop);
Check(manager.Broadcasts.All(m => m.ServerMsgType != ServerMsgType.RoomEquipmentRollResult),
    "foreign room and non-owner cannot trigger distribution");
await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => objects.RollEquipmentAsync(owner, drop)));
var awarded = manager.Broadcasts.Single(m => m.ServerMsgType == ServerMsgType.RoomEquipmentRollResult);
var winner = manager.Sessions.Single(p => p.UserId == awarded.Id);
var transmitted = Msg.Parser.ParseFrom(awarded.ToByteArray());
Check(transmitted.EquipmentRollScores.Where(s => s.Round == 1).Select(s => s.UserId)
    .OrderBy(id => id).SequenceEqual(new[] { "guest", "owner" }) &&
    transmitted.EquipmentRollScores.All(s => s.Score >= 1 && s.Score <= 100 &&
        s.Round >= 1 && s.PlayerObjectId == manager.Sessions.Single(p => p.UserId == s.UserId).NetworkObjectId),
    "structured scores identify each player and survive protobuf transport");
var finalRound = transmitted.EquipmentRollScores.Max(s => s.Round);
Check(transmitted.EquipmentRollScores.Single(s => s.Round == finalRound && s.UserId == awarded.Id).Score ==
    transmitted.EquipmentRollScores.Where(s => s.Round == finalRound).Max(s => s.Score),
    "transmitted final highest score belongs to awarded winner");
Check(owner.Data.EquipData.Count + guest.Data.EquipData.Count == 1 && winner.Data.EquipData.Count == 1,
    "concurrent repeated requests save exactly one reward to winner");
Check(outsider.Data.EquipData.Count == 0 && offline.Data.EquipData.Count == 0 &&
    !awarded.TipsSrt.Contains("outsider") && !awarded.TipsSrt.Contains("offline"),
    "other rooms and disconnected members excluded");
Check(awarded.ChannelChatMessage.Channel == ChatChannelType.Room &&
    manager.Broadcasts.Any(m => m.ServerMsgType == ServerMsgType.NetworkObjectDespawn && m.NetworkObject.ObjectId == drop),
    "result contains room history and drop is removed after persistence");
uint low = await Spawn(2);
Check(!manager.Broadcasts.Last().NetworkObject.EquipmentRollRequired, "blue equipment uses normal pickup");
await objects.ClaimAsync(guest, low);
Check(manager.Broadcasts.Last().ServerMsgType == ServerMsgType.NetworkRoomObjectClaimResult &&
    manager.Broadcasts.Last().RoleType == 1, "low quality equipment remains claimable");
NetworkRoomManager.Instance.MinQuality = 4;
await Spawn(3);
Check(!manager.Broadcasts.Last().NetworkObject.EquipmentRollRequired, "purple excluded when minimum is orange");
await Spawn(4);
Check(manager.Broadcasts.Last().NetworkObject.EquipmentRollRequired, "minimum quality included in roll");
NetworkRoomManager.Instance.MinQuality = 2;
await Spawn(2);
Check(manager.Broadcasts.Last().NetworkObject.EquipmentRollRequired, "blue can be configured to roll");
NetworkRoomManager.Instance.MinQuality = 0;
await Spawn(3);
Check(manager.Broadcasts.Last().NetworkObject.EquipmentRollRequired, "unset minimum defaults to purple");
NetworkRoomManager.Instance.RollEnabled = false;
uint disabled = await Spawn(8);
Check(!manager.Broadcasts.Last().NetworkObject.EquipmentRollRequired, "disabled room does not roll high quality equipment");
NetworkRoomManager.Instance.RollEnabled = true;
uint retry = await Spawn(4);
owner.FailSave = guest.FailSave = true;
try { await objects.RollEquipmentAsync(owner, retry); } catch (IOException) { }
var originalWinner = new[] { owner, guest }.Single(p => p.FailedSave);
Check(manager.Broadcasts.All(m => m.ServerMsgType != ServerMsgType.NetworkObjectDespawn || m.NetworkObject.ObjectId != retry),
    "failed persistence keeps drop available for retry");
owner.FailSave = guest.FailSave = false;
await objects.RollEquipmentAsync(owner, retry);
Check(owner.Data.EquipData.Count + guest.Data.EquipData.Count == 2, "retry after save failure grants one reward");
Check(manager.Broadcasts.Last(m => m.ServerMsgType == ServerMsgType.RoomEquipmentRollResult).Id == originalWinner.UserId,
    "persistence retry retains original roll winner");
uint interrupted = await Spawn(5);
owner.FailSendOnce = guest.FailSendOnce = true;
try { await objects.RollEquipmentAsync(owner, interrupted); } catch (IOException) { }
owner.FailSendOnce = guest.FailSendOnce = false;
await objects.RollEquipmentAsync(owner, interrupted);
Check(owner.Data.EquipData.Count + guest.Data.EquipData.Count == 3,
    "response failure after successful save does not reroll or duplicate reward");

namespace WebSocketDemo
{
    public sealed class PlayerSession(string id, uint room)
    {
        public string PlayerId => id;
        public string UserId => id;
        public string ServerId => "test";
        public uint NetworkRoomId { get; set; } = room;
        public uint NetworkObjectId => (uint)(id.GetHashCode() & int.MaxValue) + 1;
        public bool IsConnected { get; set; } = true;
        public UserData Data { get; } = new() { Name = id };
        public UserData UserDataSnapshot => Data;
        public bool FailSave;
        public bool FailedSave;
        public bool FailSendOnce;
        public List<Msg> Messages { get; } = new();
        public Task SendBinaryAsync(byte[] data)
        {
            if (FailSendOnce) { FailSendOnce = false; throw new IOException("simulated response failure"); }
            Messages.Add(Msg.Parser.ParseFrom(data)); return Task.CompletedTask;
        }
        public async Task<UserData> MutateAndSaveUserDataSnapshotAsync(Action<UserData> action)
        {
            await Task.Delay(10);
            if (FailSave) { FailedSave = true; throw new IOException("simulated storage failure"); }
            action(Data); return Data.Clone();
        }
        public Task WaitForSnapshotSendCapacityAsync() => Task.CompletedTask;
        public void QueueLatestTransformBatch(ulong key, byte[] bytes) { }
    }
    public sealed class PlayerSessionManager
    {
        public static PlayerSessionManager Instance { get; } = new();
        public List<PlayerSession> Sessions { get; } = new();
        public List<Msg> Broadcasts { get; } = new();
        private uint nextId = 100;
        public uint AllocateNetworkObjectId() => ++nextId;
        public PlayerSession? GetSession(string id) => Sessions.FirstOrDefault(p => p.PlayerId == id);
        public PlayerSession[] GetRoomSessions(uint room) => Sessions.Where(p => p.NetworkRoomId == room).ToArray();
        public IEnumerable<PlayerSession> SessionsForRoom(uint room) => GetRoomSessions(room);
        public bool HasRoomObserver(uint room, string id) => true;
        public Task BroadcastBinaryAsync(byte[] data, string? excluded, uint room)
        { Broadcasts.Add(Msg.Parser.ParseFrom(data)); return Task.CompletedTask; }
        public void BroadcastLatestBinary(ulong key, byte[] bytes, string id, uint room, int maxAgeMs = 0) { }
        public static ulong CreateRealtimeKey(ServerMsgType type, uint id, int track = 0) => id;
    }
    public sealed class NetworkRoomManager
    {
        public const uint LobbyRoomId = 1;
        public static NetworkRoomManager Instance { get; } = new();
        public bool RollEnabled = true;
        public int MinQuality = 3;
        public bool TryGetRoomData(uint room, out NetworkRoomData data)
        { data = new() { RoomId = room, HighQualityEquipmentRoll = RollEnabled,
            EquipmentRollMinQuality = MinQuality }; return true; }
        public bool IsHost(PlayerSession player) => player.PlayerId == "owner";
        public uint GetSceneEpoch(uint room) => 1;
    }
    public sealed class NetworkSyncVarManager
    {
        public static NetworkSyncVarManager Instance { get; } = new();
        public void RemoveObject(uint id) { }
        public void AppendObjectStates(uint room, uint id, List<NetworkSyncVarData> states) { }
    }
}
