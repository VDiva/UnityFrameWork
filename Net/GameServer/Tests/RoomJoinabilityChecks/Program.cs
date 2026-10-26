using GameData;
using Google.Protobuf;
using WebSocketDemo;

static void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}
var manager = NetworkRoomManager.Instance;
var host = new PlayerSession("host");
var guest = new PlayerSession("guest");
var newcomer = new PlayerSession("newcomer");
Check(manager.TryCreate(host, new NetworkRoomData { MapName = "ReadyScene", LevelKey = "level", MaxPlayers = 2 }, out var room, out _), "create waiting room");
uint id = room.RoomId;
Check(manager.TryCompleteJoin(host, id, out room, out _, out _) && room.CanJoin && !room.Started, "waiting room accepts players");
var settings = room.Clone();
settings.HighQualityEquipmentRoll = true;
settings.EquipmentRollMinQuality = 4;
Check(manager.TryUpdateRoom(host, settings, out room, out _) && room.HighQualityEquipmentRoll,
    "host can enable equipment rolling while waiting");
Check(NetworkRoomData.Parser.ParseFrom(room.ToByteArray()).HighQualityEquipmentRoll,
    "equipment distribution setting survives protobuf round trip");
Check(room.EquipmentRollMinQuality == 4 &&
    NetworkRoomData.Parser.ParseFrom(room.ToByteArray()).EquipmentRollMinQuality == 4,
    "configured minimum quality saved and synchronized");
Check(manager.TryReserveJoin(guest, id, out room, out _) && !room.CanJoin && room.PlayerCount == 1, "reservation consumes final slot before loading completes");
Check(!manager.TryReserveJoin(newcomer, id, out _, out _), "full room rejects join");
Check(!manager.TryBeginMapChange(host, "GameScene", out _, out _, out _), "pending join blocks map change");
Check(!manager.TryStart(host, out _, out _), "pending join blocks start");
Check(manager.TryCompleteJoin(guest, id, out room, out _, out _) && !room.CanJoin, "full waiting room remains closed");
settings.HighQualityEquipmentRoll = false;
Check(!manager.TryUpdateRoom(guest, settings, out _, out _), "guest cannot change distribution mode");
manager.Leave(guest);
Check(manager.TryGetRoomData(id, out room) && room.CanJoin, "leaving reopens waiting room");
Check(manager.TryBeginMapChange(host, "GameScene", out room, out _, out _) && room.Started && !room.CanJoin, "battle transition immediately closes room");
Check(manager.TryCompleteJoin(host, id, out room, out var allEntered, out _) && allEntered && room.Started && !room.CanJoin, "battle stays closed after loading");
Check(room.HighQualityEquipmentRoll && !manager.TryUpdateRoom(host, settings, out _, out _),
    "distribution mode retained and locked during battle");
Check(!manager.TryReserveJoin(newcomer, id, out _, out _), "battle rejects direct join");
Check(!manager.TryReserveInvite(newcomer, id, out _, out _), "battle rejects invitation");
Check(!manager.GetJoinableRooms(host.ServerId).Any(r => r.RoomId == id), "battle excluded from matching list");
Check(manager.TryBeginMapChange(host, "ReadyScene", out room, out _, out _) && !room.Started && !room.CanJoin, "return loading remains closed");
Check(!manager.TryReserveJoin(newcomer, id, out _, out _), "return loading rejects join");
Check(manager.TryCompleteJoin(host, id, out room, out _, out _) && room.CanJoin && !room.Started, "return complete reopens room");
Check(NetworkRoomData.Parser.ParseFrom(room.ToByteArray()).CanJoin, "CanJoin survives protobuf round trip");
Check(manager.GetJoinableRooms(host.ServerId).Any(r => r.RoomId == id && r.CanJoin), "waiting room listed again");
Check(manager.TrySetReady(host, true, out _, out _) && manager.TryStart(host, out room, out _) && room.Started && !room.CanJoin, "explicit start also closes room");
var matchingHost = new PlayerSession("matching-host");
Check(manager.TryMatch(matchingHost, new NetworkRoomData
    { MapName = "ReadyScene", LevelKey = "level", HighQualityEquipmentRoll = true }, out var rollRoom, out _),
    "matching creates room with requested distribution mode");
Check(rollRoom.EquipmentRollMinQuality == 3, "unset room threshold defaults to purple");
var orangeMatch = new PlayerSession("orange-match");
Check(manager.TryMatch(orangeMatch, new NetworkRoomData
    { MapName = "ReadyScene", LevelKey = "level", HighQualityEquipmentRoll = true,
      EquipmentRollMinQuality = 4 }, out var orangeRoom, out _) &&
    orangeRoom.RoomId != rollRoom.RoomId && orangeRoom.EquipmentRollMinQuality == 4,
    "matching separates different quality thresholds");
var normalMatch = new PlayerSession("normal-match");
Check(manager.TryMatch(normalMatch, new NetworkRoomData
    { MapName = "ReadyScene", LevelKey = "level" }, out var normalRoom, out _) &&
    normalRoom.RoomId != rollRoom.RoomId && !normalRoom.HighQualityEquipmentRoll,
    "matching keeps different distribution modes in separate rooms");

// Only transport/session storage is substituted; tests execute the production room manager and protocol.
namespace WebSocketDemo
{
    public sealed class PlayerSession
    {
        public PlayerSession(string id) { PlayerId = id; PlayerSessionManager.Instance.Sessions[id] = this; }
        public string PlayerId { get; }
        public string UserId => PlayerId;
        public string ServerId => ServerScope.DefaultServerId;
        public uint NetworkRoomId { get; set; }
        public uint PendingNetworkRoomId { get; set; }
        public uint NetworkObjectId => 0;
        public bool IsConnected => true;
        public UserData? UserDataSnapshot => null;
    }
    public sealed class PlayerSessionManager
    {
        public static PlayerSessionManager Instance { get; } = new();
        public Dictionary<string, PlayerSession> Sessions { get; } = new();
        public PlayerSession? GetSession(string id) => Sessions.GetValueOrDefault(id);
        public void ForgetRoom(uint roomId) { }
    }
    public static class ServerScope
    {
        public const string DefaultServerId = "test";
        public static string Normalize(string? value) => value ?? DefaultServerId;
    }
    public sealed class RoomSharedValueManager
    {
        public static RoomSharedValueManager Instance { get; } = new();
        public void RemoveRoom(string serverId, uint roomId) { }
    }
    public sealed class NetworkRoomObjectManager
    {
        public static NetworkRoomObjectManager Instance { get; } = new();
        public Task DestroyRoomObjectsAsync(uint roomId) => Task.CompletedTask;
    }
}
