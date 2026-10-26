using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading.Channels;
using GameData;
using Google.Protobuf;
using WebSocketDemo;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}

var socket = new BlockingWebSocket();
using var session = new PlayerSession("batch-check", socket);
await session.SendBinaryAsync(new byte[] { 0 });
await socket.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

static byte[] Batch(params NetworkTransformData[] snapshots)
{
    var msg = new Msg { MsgType = ProtobufMsgType.Server,
        ServerMsgType = ServerMsgType.NetworkTransformUpdate };
    msg.NetworkTransforms.Add(snapshots);
    return msg.ToByteArray();
}

const ulong key = (7UL << 32) | 0x40000000UL;
session.QueueLatestTransformBatch(key, Batch(
    new NetworkTransformData { ObjectId = 900, PositionX = 1 },
    new NetworkTransformData { ObjectId = 901, PositionX = 2 }));
session.QueueLatestTransformBatch(key, Batch(
    new NetworkTransformData { ObjectId = 900, PositionX = 3 }));
socket.ReleaseFirstSend.TrySetResult(true);
await socket.TwoSendsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

Check(socket.Sent.Count == 2, "congested batch becomes one queued packet");
Msg merged = Msg.Parser.ParseFrom(socket.Sent.Last());
Check(merged.NetworkTransforms.Count == 2 &&
      merged.NetworkTransforms.Any(s => s.ObjectId == 900 && s.PositionX == 3) &&
      merged.NetworkTransforms.Any(s => s.ObjectId == 901 && s.PositionX == 2),
    "batch merge retains every object's latest position");

var manager = PlayerSessionManager.Instance;
Check(manager.TryAddSession(session.PlayerId, session), "session registered");
PropertyInfo roomProperty = typeof(PlayerSession).GetProperty(nameof(PlayerSession.NetworkRoomId))!;
roomProperty.SetValue(session, 42u);
Check(manager.GetRoomSessions(42).SingleOrDefault() == session, "room index includes joined player");
roomProperty.SetValue(session, 43u);
Check(manager.GetRoomSessions(42).Length == 0 &&
      manager.GetRoomSessions(43).SingleOrDefault() == session,
    "room index moves player without stale membership");
roomProperty.SetValue(session, 0u);
Check(manager.GetRoomSessions(43).Length == 0, "room index clears on leave");
await manager.RemoveSessionAsync(session.PlayerId);

var incomingSocket = new BlockingWebSocket();
using var incomingSession = new PlayerSession("receive-check", incomingSocket);
var largeRequest = new Msg { MsgType = ProtobufMsgType.Game,
    GameMsgType = GameMsgType.GetServerList };
largeRequest.DataDic["__server_version_query"] = "1";
largeRequest.DataDic["__web_request_id"] = "large";
largeRequest.DataDic["padding"] = new string('x', 80 * 1024);
var smallRequest = new Msg { MsgType = ProtobufMsgType.Game,
    GameMsgType = GameMsgType.GetServerList };
smallRequest.DataDic["__server_version_query"] = "1";
smallRequest.DataDic["__latency_probe"] = "1";
smallRequest.DataDic["__web_request_id"] = "small";
incomingSocket.Feed(largeRequest.ToByteArray());
incomingSocket.Feed(smallRequest.ToByteArray());
await incomingSocket.TwoSendsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
string[] responseIds = incomingSocket.Sent
    .Select(bytes => Msg.Parser.ParseFrom(bytes).DataDic["__web_response"]).ToArray();
Check(responseIds.SequenceEqual(new[] { "large", "small" }),
    "fragmented large message and shorter next message parse independently");
Msg probeResponse = Msg.Parser.ParseFrom(incomingSocket.Sent.Last());
Check(probeResponse.DataDic.ContainsKey(ServerVersionProtocol.QueuePacketsKey) &&
      probeResponse.DataDic.ContainsKey(ServerVersionProtocol.QueueBytesKey) &&
      probeResponse.DataDic.ContainsKey(ServerVersionProtocol.QueueWaitMsKey) &&
      probeResponse.DataDic.ContainsKey(ServerVersionProtocol.WriteMsKey),
    "latency probe reports server send queue and write timings");

var ownerSocket = new BlockingWebSocket();
var guestASocket = new BlockingWebSocket();
var guestBSocket = new BlockingWebSocket();
using var owner = new PlayerSession("ai-owner", ownerSocket);
using var guestA = new PlayerSession("ai-guest-a", guestASocket);
using var guestB = new PlayerSession("ai-guest-b", guestBSocket);
foreach (PlayerSession player in new[] { owner, guestA, guestB })
{
    Check(manager.TryAddSession(player.PlayerId, player), "AI test session registered");
    typeof(PlayerSession).GetProperty(nameof(PlayerSession.UserId))!
        .SetValue(player, player.PlayerId);
}
var rooms = NetworkRoomManager.Instance;
Check(rooms.TryCreate(owner, new NetworkRoomData { MapName = NetworkRoomManager.ReadyMapName,
    LevelKey = "ai-test", MaxPlayers = 3 }, out var aiRoom, out _), "AI room created");
uint aiRoomId = aiRoom.RoomId;
uint aiEpoch = rooms.GetSceneEpoch(aiRoomId);
Check(rooms.TryCompleteJoin(owner, aiRoomId, out _, out _, out _), "AI owner joined");
foreach (PlayerSession guest in new[] { guestA, guestB })
{
    Check(rooms.TryReserveInvite(guest, aiRoomId, out _, out _) &&
          rooms.TryCompleteJoin(guest, aiRoomId, out _, out _, out _),
        "AI guest joined");
}
await Task.WhenAll(Enumerable.Range(0, 60).Select(i => Task.Run(() =>
    NetworkRoomObjectManager.Instance.SpawnAiAsync(owner,
        new NetworkObjectData { PrefabId = "ai-test" },
        new NetworkTransformData { PositionX = i }))));
await WaitForMessagesAsync(ownerSocket, ServerMsgType.NetworkObjectSpawn, 60);
var spawnOwners = ownerSocket.Sent.Select(Msg.Parser.ParseFrom)
    .Where(msg => msg.ServerMsgType == ServerMsgType.NetworkObjectSpawn &&
                  msg.NetworkObject?.AiObject == true)
    .GroupBy(msg => msg.NetworkObject.OwnerPlayerId)
    .ToDictionary(group => group.Key, group => group.Count());
Check(new[] { owner, guestA, guestB }.All(player =>
        spawnOwners.GetValueOrDefault(player.PlayerId) == 20),
    "60 concurrent AI spawns distribute evenly across three players");
var lateSocket = new BlockingWebSocket();
using var lateReceiver = new PlayerSession("late-ai-receiver", lateSocket);
roomProperty.SetValue(lateReceiver, aiRoomId);
await NetworkRoomObjectManager.Instance.SendRoomObjectsToAsync(lateReceiver);
await WaitForMessagesAsync(lateSocket, ServerMsgType.NetworkObjectSpawn, 2);
Msg[] initialBatches = lateSocket.Sent.Select(Msg.Parser.ParseFrom)
    .Where(msg => msg.ServerMsgType == ServerMsgType.NetworkObjectSpawn).ToArray();
Check(initialBatches.Length == 2 && initialBatches.All(msg => msg.NetworkObjects.Count <= 32) &&
      initialBatches.Sum(msg => msg.NetworkObjects.Count) == 60 &&
      initialBatches.Sum(msg => msg.NetworkTransforms.Count) == 60,
    "late join batches 60 AI spawns and initial transforms into two packets");
uint[] ownedIds = ownerSocket.Sent.Select(Msg.Parser.ParseFrom)
    .Where(msg => msg.ServerMsgType == ServerMsgType.NetworkObjectSpawn &&
                  msg.NetworkObject?.OwnerPlayerId == owner.PlayerId)
    .Select(msg => msg.NetworkObject.ObjectId).Take(2).ToArray();
Check(ownedIds.Length == 2, "two owned AI objects available for sync snapshot check");
manager.EnsureNetworkObjectId(owner);
uint guestOwnedId = ownerSocket.Sent.Select(Msg.Parser.ParseFrom)
    .Where(msg => msg.ServerMsgType == ServerMsgType.NetworkObjectSpawn &&
                  msg.NetworkObject?.OwnerPlayerId == guestA.PlayerId)
    .Select(msg => msg.NetworkObject!.ObjectId).First();
var transformBatch = new Msg { MsgType = ProtobufMsgType.Game,
    GameMsgType = GameMsgType.UploadNetworkTransform };
transformBatch.NetworkTransforms.Add(new NetworkTransformData { ObjectId = ownedIds[0],
    RoomId = aiRoomId, SceneEpoch = aiEpoch, PositionX = 11, RotationW = 1, Sequence = 123 });
transformBatch.NetworkTransforms.Add(new NetworkTransformData { ObjectId = guestOwnedId,
    RoomId = aiRoomId, SceneEpoch = aiEpoch, PositionX = 99, RotationW = 1, Sequence = 123 });
transformBatch.NetworkTransforms.Add(new NetworkTransformData { ObjectId = ownedIds[1],
    RoomId = aiRoomId, SceneEpoch = aiEpoch, PositionX = 22, RotationW = 1, Sequence = 123 });
transformBatch.NetworkTransforms.Add(new NetworkTransformData { ObjectId = ownedIds[0],
    RoomId = aiRoomId + 1, SceneEpoch = aiEpoch, PositionX = 100, RotationW = 1, Sequence = 124 });
transformBatch.NetworkTransforms.Add(new NetworkTransformData { ObjectId = ownedIds[1],
    RoomId = aiRoomId, SceneEpoch = aiEpoch, PositionX = 200, RotationW = 1, Sequence = 122 });
transformBatch.NetworkTransforms.Add(new NetworkTransformData { ObjectId = ownedIds[1],
    RoomId = aiRoomId, SceneEpoch = aiEpoch + 1, PositionX = 300, RotationW = 1, Sequence = 124 });
await MsgMrg.Received(transformBatch, owner);
using (var transformWait = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
    while (guestASocket.Sent.Select(Msg.Parser.ParseFrom).Where(msg =>
               msg.ServerMsgType == ServerMsgType.NetworkTransformUpdate)
           .SelectMany(msg => msg.NetworkTransforms).Count(snapshot => snapshot.Sequence == 123) < 2)
        await Task.Delay(5, transformWait.Token);
NetworkTransformData[] relayedTransforms = guestASocket.Sent.Select(Msg.Parser.ParseFrom)
    .Where(msg => msg.ServerMsgType == ServerMsgType.NetworkTransformUpdate)
    .SelectMany(msg => msg.NetworkTransforms).Where(snapshot => snapshot.Sequence == 123).ToArray();
Check(relayedTransforms.Length == 2 &&
      relayedTransforms.Any(t => t.ObjectId == ownedIds[0] && t.PositionX == 11) &&
      relayedTransforms.Any(t => t.ObjectId == ownedIds[1] && t.PositionX == 22),
    "AI batch rejects foreign, cross-room, cross-scene and stale positions");
var sync = NetworkSyncVarManager.Instance;
await sync.RelayAsync(owner, new NetworkSyncVarData { ObjectId = ownedIds[0],
    BehaviourId = 1, FieldId = 1, Sequence = 2, Value = ByteString.CopyFromUtf8("latest") });
await sync.RelayAsync(owner, new NetworkSyncVarData { ObjectId = ownedIds[0],
    BehaviourId = 1, FieldId = 1, Sequence = 1, Value = ByteString.CopyFromUtf8("stale") });
await sync.RelayAsync(guestA, new NetworkSyncVarData { ObjectId = ownedIds[0],
    BehaviourId = 1, FieldId = 1, Sequence = 99, Value = ByteString.CopyFromUtf8("hijack") });
await sync.RelayAsync(owner, new NetworkSyncVarData { ObjectId = ownedIds[1],
    BehaviourId = 1, FieldId = 1, Sequence = 1, Value = ByteString.CopyFromUtf8("other") });
await NetworkRoomObjectManager.Instance.RelayAnimationAsync(owner,
    new NetworkAnimationData { ObjectId = ownedIds[0], TrackIndex = 1, Sequence = 1 });
await NetworkRoomObjectManager.Instance.SendRoomObjectsToAsync(lateReceiver);
await WaitForMessagesAsync(lateSocket, ServerMsgType.NetworkObjectStateBatch, 1);
Msg initialState = lateSocket.Sent.Select(Msg.Parser.ParseFrom)
    .Last(msg => msg.ServerMsgType == ServerMsgType.NetworkObjectStateBatch);
Check(initialState.NetworkSyncVars.Count == 2 &&
      initialState.NetworkSyncVars.Any(state => state.ObjectId == ownedIds[0] &&
          state.Sequence == 2 && state.Value.ToStringUtf8() == "latest") &&
      initialState.NetworkSyncVars.Any(state => state.ObjectId == ownedIds[1] &&
          state.Value.ToStringUtf8() == "other") &&
      initialState.NetworkAnimations.Any(state => state.ObjectId == ownedIds[0] &&
          state.TrackIndex == 1),
    "late join batches the newest animation and sync var state after spawns");
var syncSocket = new BlockingWebSocket();
using var syncReceiver = new PlayerSession("sync-receiver", syncSocket);
roomProperty.SetValue(syncReceiver, aiRoomId);
await sync.SendObjectStateAsync(syncReceiver, ownedIds[0]);
await WaitForMessagesAsync(syncSocket, ServerMsgType.NetworkSyncVarUpdate, 1);
Msg onlyState = Msg.Parser.ParseFrom(syncSocket.Sent.Single());
Check(onlyState.NetworkSyncVar.ObjectId == ownedIds[0] &&
      onlyState.NetworkSyncVar.Sequence == 2 &&
      onlyState.NetworkSyncVar.Value.ToStringUtf8() == "latest",
    "late join gets only requested object's newest sync var");
sync.RemoveObject(ownedIds[0]);
await sync.SendObjectStateAsync(syncReceiver, ownedIds[0]);
Check(syncSocket.Sent.Count == 1, "removed object's sync var is absent");
sync.RemoveObject(ownedIds[1]);
rooms.Leave(owner);
await NetworkRoomObjectManager.Instance.HandleOwnerLeavingAsync(owner, aiRoomId);
await WaitForMessagesAsync(guestASocket, ServerMsgType.NetworkObjectAuthorityChanged, 20);
var transferredOwners = guestASocket.Sent.Select(Msg.Parser.ParseFrom)
    .Where(msg => msg.ServerMsgType == ServerMsgType.NetworkObjectAuthorityChanged)
    .GroupBy(msg => msg.NetworkObject.OwnerPlayerId)
    .ToDictionary(group => group.Key, group => group.Count());
Check(transferredOwners.GetValueOrDefault(guestA.PlayerId) == 10 &&
      transferredOwners.GetValueOrDefault(guestB.PlayerId) == 10,
    "owner departure redistributes AI evenly to remaining players");
await NetworkRoomObjectManager.Instance.DestroyRoomObjectsAsync(aiRoomId, notifyClients: false);
rooms.Leave(guestA);
rooms.Leave(guestB);
foreach (PlayerSession player in new[] { owner, guestA, guestB })
{
    player.ClearUserBinding();
    await manager.RemoveSessionAsync(player.PlayerId);
}

static async Task WaitForMessagesAsync(BlockingWebSocket socket, ServerMsgType type, int count)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (socket.Sent.Count(bytes => Msg.Parser.ParseFrom(bytes).ServerMsgType == type) < count)
        await Task.Delay(5, timeout.Token);
}

sealed class BlockingWebSocket : WebSocket
{
    private readonly Channel<(byte[] Data, bool EndOfMessage)> _incoming =
        Channel.CreateUnbounded<(byte[], bool)>();
    private WebSocketState _state = WebSocketState.Open;
    public readonly TaskCompletionSource<bool> FirstSendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource<bool> ReleaseFirstSend = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource<bool> TwoSendsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly ConcurrentQueue<byte[]> Sent = new();

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;
    public override void Abort() => _state = WebSocketState.Aborted;
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken) => CloseAsync(closeStatus, statusDescription, cancellationToken);
    public override void Dispose() => _state = WebSocketState.Closed;
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        var frame = await _incoming.Reader.ReadAsync(cancellationToken);
        frame.Data.CopyTo(buffer.Array!, buffer.Offset);
        return new WebSocketReceiveResult(frame.Data.Length, WebSocketMessageType.Binary,
            frame.EndOfMessage);
    }
    public void Feed(byte[] message)
    {
        for (int offset = 0; offset < message.Length; offset += 2048)
        {
            int count = Math.Min(2048, message.Length - offset);
            _incoming.Writer.TryWrite((message.AsSpan(offset, count).ToArray(),
                offset + count == message.Length));
        }
    }
    public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken)
    {
        if (buffer.Count == 1 && buffer.Array![buffer.Offset] == 0)
        {
            FirstSendStarted.TrySetResult(true);
            await ReleaseFirstSend.Task.WaitAsync(cancellationToken);
        }
        Sent.Enqueue(buffer.ToArray());
        if (Sent.Count == 2) TwoSendsCompleted.TrySetResult(true);
    }
}
