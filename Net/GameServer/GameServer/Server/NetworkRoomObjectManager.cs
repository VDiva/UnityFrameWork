using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;
using System.Security.Cryptography;
using System.Text.Json;

namespace WebSocketDemo;

/// <summary>管理非玩家房间网络物体，ID、房间和所有权均以服务端记录为准。</summary>
public sealed class NetworkRoomObjectManager
{
    private const int MaxPrefabIdLength = 128;
    private const int MaxTriggerIdLength = 128;
    private const int DropRoleType = 5;
    private const int InitialObjectsPerPacket = 32;
    private const int InitialStatePacketBytes = 48 * 1024;
    private readonly ConcurrentDictionary<uint, RoomObject> _objects = new();
    private readonly object _objectIndexGate = new();
    private readonly Dictionary<uint, Dictionary<uint, RoomObject>> _objectsByRoom = new();
    private bool AddObject(uint id, RoomObject obj)
    {
        lock (_objectIndexGate)
        {
            if (!_objects.TryAdd(id, obj)) return false;
            if (!_objectsByRoom.TryGetValue(obj.RoomId, out var room))
                _objectsByRoom[obj.RoomId] = room = new();
            room[id] = obj;
            return true;
        }
    }
    private bool RemoveObject(uint id, out RoomObject? obj)
    {
        lock (_objectIndexGate)
        {
            if (!_objects.TryRemove(id, out obj)) return false;
            if (_objectsByRoom.TryGetValue(obj.RoomId, out var room))
            {
                room.Remove(id);
                if (room.Count == 0) _objectsByRoom.Remove(obj.RoomId);
            }
            return true;
        }
    }
    private RoomObject[] RoomObjects(uint roomId)
    {
        lock (_objectIndexGate)
            return _objectsByRoom.TryGetValue(roomId, out var room) ? room.Values.ToArray() : Array.Empty<RoomObject>();
    }
    private readonly ConcurrentDictionary<uint, AiOwnerLoad> _aiOwnerLoads = new();
    private readonly ConcurrentDictionary<uint, ObjectClaim> _claims = new();
    private readonly ConcurrentDictionary<uint, ConcurrentDictionary<string, byte>> _triggeredAiSpawns = new();
    public static NetworkRoomObjectManager Instance { get; } = new();

    private sealed class RoomObject
    {
        public uint ObjectId;
        public uint RoomId;
        public string PrefabId = string.Empty;
        public string OwnerSessionId = string.Empty;
        public string MonsterBoxKey = string.Empty;
        public string TriggerId = string.Empty;
        public int BoxIndex;
        public int RoleType;
        public bool IsAi;
        public EquipData? EquipmentDrop;
        public bool EquipmentRollRequired;
        public bool EquipmentRollStarted;
        public PlayerSession[]? EquipmentRollPlayers;
        public EquipmentRoll.Result? EquipmentRollResult;
        public bool EquipmentAwarded;
        public NetworkTransformData? Transform;
        public string TransformOwnerSessionId = string.Empty;
        public ConcurrentDictionary<int, NetworkAnimationData> Animations = new();
    }

    private sealed record ObjectClaim(string SessionId, string UserId, uint PlayerObjectId);

    private sealed class AiOwnerLoad
    {
        public readonly object Gate = new();
        public readonly Dictionary<string, int> Counts;
        public AiOwnerLoad(Dictionary<string, int> counts) => Counts = counts;
    }

    public async Task SpawnAsync(PlayerSession owner, NetworkObjectData? request, NetworkTransformData? transform)
    {
        if (owner.NetworkRoomId == 0 || owner.NetworkRoomId == NetworkRoomManager.LobbyRoomId ||
            request == null || string.IsNullOrWhiteSpace(request.PrefabId) || request.PrefabId.Length > MaxPrefabIdLength)
            return;

        if (transform != null && !NetworkStateValidation.IsValid(transform)) return;
        uint objectId = PlayerSessionManager.Instance.AllocateNetworkObjectId();
        var roomObject = new RoomObject { ObjectId = objectId, RoomId = owner.NetworkRoomId,
            PrefabId = request.PrefabId.Trim(), OwnerSessionId = owner.PlayerId,
            RoleType = request.RoleType, IsAi = false };
        if (request.EquipmentDrop != null && roomObject.RoleType == DropRoleType)
        {
            // 沿用掉落生成方提供的装备内容；品质和分配模式由服务端判断。
            if (!TryReadEquipmentQuality(request.EquipmentDrop, out int quality)) return;
            roomObject.EquipmentDrop = request.EquipmentDrop.Clone();
            roomObject.EquipmentDrop.Id = Guid.NewGuid().ToString("N");
            roomObject.EquipmentRollRequired =
                NetworkRoomManager.Instance.TryGetRoomData(roomObject.RoomId, out var room) &&
                room.HighQualityEquipmentRoll &&
                quality >= (room.EquipmentRollMinQuality == 0 ? 3 : room.EquipmentRollMinQuality);
        }
        if (!AddObject(objectId, roomObject)) return;

        var objectData = new NetworkObjectData { ObjectId = objectId, RoomId = roomObject.RoomId,
            PrefabId = roomObject.PrefabId, OwnerPlayerId = owner.UserId ?? string.Empty,
            PlayerObject = false, AiObject = false, RoleType = roomObject.RoleType,
            SpawnRequestId = request.SpawnRequestId };
        objectData.EquipmentDrop = roomObject.EquipmentDrop?.Clone();
        objectData.EquipmentRollRequired = roomObject.EquipmentRollRequired;
        NetworkTransformData? initialTransform = null;
        if (transform != null)
        {
            initialTransform = transform.Clone();
            initialTransform.ObjectId = objectId;
            initialTransform.RoomId = roomObject.RoomId;
            initialTransform.SceneEpoch = NetworkRoomManager.Instance.GetSceneEpoch(roomObject.RoomId);
            roomObject.Transform = initialTransform.Clone();
        }
#if NETWORK_SPAWN_TRACE
        Console.WriteLine(
            $"[NetworkSpawn] prefabId={objectData.PrefabId}, objectId={objectData.ObjectId}, " +
            $"requestId={objectData.SpawnRequestId}, owner={objectData.OwnerPlayerId}");
#endif
        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectSpawn, NetworkObject = objectData,
            NetworkTransform = initialTransform }.ToByteArray(), null, roomObject.RoomId);

    }

    public async Task SpawnAiAsync(PlayerSession requester, NetworkObjectData? request, NetworkTransformData? transform)
    {
        if (!NetworkRoomManager.Instance.IsHost(requester) || requester.NetworkRoomId == 0 ||
            request == null || string.IsNullOrWhiteSpace(request.PrefabId) || request.PrefabId.Length > MaxPrefabIdLength)
            return;

        await SpawnAiForRoomAsync(requester.NetworkRoomId, request, transform);
    }

    /// <summary>
    /// 任意游戏房间成员可以报告关卡触发器；同一房间的相同 TriggerId 通过原子去重只生成一次。
    /// </summary>
    public async Task SpawnTriggeredAiAsync(
        PlayerSession requester, NetworkObjectData? request, NetworkTransformData? transform)
    {
        uint roomId = requester.NetworkRoomId;
        if (roomId == 0 || roomId == NetworkRoomManager.LobbyRoomId ||
            request == null || string.IsNullOrWhiteSpace(request.TriggerId) ||
            request.TriggerId.Length > MaxTriggerIdLength ||
            string.IsNullOrWhiteSpace(request.PrefabId) || request.PrefabId.Length > MaxPrefabIdLength)
            return;

        string triggerId = request.TriggerId.Trim();
        ConcurrentDictionary<string, byte> roomTriggers =
            _triggeredAiSpawns.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        if (!roomTriggers.TryAdd(triggerId, 0))
            return;

        // 如果房间此刻没有可接管 AI 的玩家，则允许该触发器稍后重新请求。
        if (!await SpawnAiForRoomAsync(roomId, request, transform))
            roomTriggers.TryRemove(triggerId, out _);
    }

    async Task<bool> SpawnAiForRoomAsync(
        uint roomId, NetworkObjectData request, NetworkTransformData? transform)
    {

        if (transform != null && !NetworkStateValidation.IsValid(transform)) return false;
        PlayerSession[] roomSessions = PlayerSessionManager.Instance.GetRoomSessions(roomId);
        if (roomSessions.Length == 0) return false;
        AiOwnerLoad load = GetAiOwnerLoad(roomId);
        RoomObject obj;
        lock (load.Gate)
        {
            PlayerSession? owner = SelectLeastLoadedAiOwner(roomSessions, load.Counts, roomId);
            if (owner == null) return false;
            uint objectId = PlayerSessionManager.Instance.AllocateNetworkObjectId();
            obj = new RoomObject { ObjectId = objectId, RoomId = roomId,
                PrefabId = request.PrefabId.Trim(), OwnerSessionId = owner.PlayerId,
                MonsterBoxKey = request.MonsterBoxKey, BoxIndex = request.BoxIndex,
                TriggerId = request.TriggerId,
                RoleType = request.RoleType, IsAi = true };
            if (!AddObject(objectId, obj)) return false;
            AdjustAiOwnerCount(load.Counts, owner.PlayerId, 1);
        }

        NetworkTransformData? initialTransform = null;
        if (transform != null)
        {
            initialTransform = transform.Clone();
            initialTransform.ObjectId = obj.ObjectId;
            initialTransform.RoomId = roomId;
            initialTransform.SceneEpoch = NetworkRoomManager.Instance.GetSceneEpoch(roomId);
            obj.Transform = initialTransform.Clone();
        }

        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectSpawn,
            NetworkObject = ToNetworkData(obj, request.SpawnRequestId),
            NetworkTransform = initialTransform }.ToByteArray(), null, obj.RoomId);
        return true;
    }

    public async Task DestroyAsync(PlayerSession requester, uint objectId)
    {
        if (!_objects.TryGetValue(objectId, out RoomObject? obj) || requester.NetworkRoomId != obj.RoomId)
            return;
        bool isClaimWinner = _claims.TryGetValue(objectId, out ObjectClaim? claim) &&
                             claim.SessionId == requester.PlayerId;
        if (obj.OwnerSessionId != requester.PlayerId && !isClaimWinner &&
            !NetworkRoomManager.Instance.IsHost(requester))
            return;
        if (RemoveObject(objectId, out _))
        {
            RemoveAiOwnerLoad(obj);
            _claims.TryRemove(objectId, out _);
            NetworkSyncVarManager.Instance.RemoveObject(objectId);
            await BroadcastDestroy(obj);
        }
    }

    /// <summary>服务器验证掉落物有效性并原子决定归属；拾取范围由客户端触发器控制。</summary>
    public async Task ClaimAsync(PlayerSession requester, uint objectId)
    {
        if (objectId == 0 || requester.NetworkRoomId == 0 ||
            requester.NetworkRoomId == NetworkRoomManager.LobbyRoomId ||
            requester.NetworkObjectId == 0 || string.IsNullOrWhiteSpace(requester.UserId) ||
            !_objects.TryGetValue(objectId, out RoomObject? obj) ||
            obj.RoomId != requester.NetworkRoomId || obj.IsAi || obj.RoleType != DropRoleType ||
            obj.EquipmentRollRequired)
        {
            await SendClaimResultAsync(requester, objectId, null, false);
            return;
        }

        var candidate = new ObjectClaim(requester.PlayerId, requester.UserId, requester.NetworkObjectId);
        bool won = _claims.TryAdd(objectId, candidate);
        ObjectClaim? winner = won
            ? candidate
            : (_claims.TryGetValue(objectId, out ObjectClaim? existing) ? existing : null);

        if (won)
        {
            // 先把掉落物控制权转给获胜玩家，再发送抢占结果。
            // 这样获胜客户端收到结果时已经可以驱动 MoveTool/Transform 同步。
            obj.OwnerSessionId = requester.PlayerId;
            NetworkSyncVarManager.Instance.RemoveObject(objectId);
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkObjectAuthorityChanged,
                NetworkObject = ToNetworkData(obj)
            }.ToByteArray(), null, obj.RoomId);

            await PlayerSessionManager.Instance.BroadcastBinaryAsync(
                CreateClaimResult(objectId, winner, true).ToByteArray(), null, obj.RoomId);
            return;
        }

        await SendClaimResultAsync(requester, objectId, winner, false);
    }

    private static bool TryReadEquipmentQuality(EquipData equip, out int quality)
    {
        quality = 0;
        if (equip.Data.Length == 0 || equip.Data.Length > 64 * 1024) return false;
        try
        {
            using var json = JsonDocument.Parse(equip.Data.ToStringUtf8());
            return json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("equipKey", out var key) &&
                key.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(key.GetString()) &&
                json.RootElement.TryGetProperty("pz", out var pz) && pz.TryGetInt32(out quality) &&
                quality >= 1 && quality <= 8;
        }
        catch (JsonException) { return false; }
    }

    public async Task RollEquipmentAsync(PlayerSession requester, uint objectId)
    {
        if (!_objects.TryGetValue(objectId, out var obj) || obj.RoomId != requester.NetworkRoomId ||
            obj.OwnerSessionId != requester.PlayerId || !obj.EquipmentRollRequired ||
            obj.EquipmentDrop == null || _claims.ContainsKey(objectId)) return;
        lock (obj)
        {
            if (obj.EquipmentRollStarted) return;
            obj.EquipmentRollStarted = true;
        }
        try
        {
            var players = obj.EquipmentRollPlayers ?? PlayerSessionManager.Instance.GetRoomSessions(obj.RoomId)
                .Where(p => p.IsConnected && p.NetworkObjectId != 0 &&
                    !string.IsNullOrWhiteSpace(p.UserId)).ToArray();
            if (players.Length == 0) return;
            var rounds = obj.EquipmentRollResult ?? EquipmentRoll.Resolve(players.Length,
                () => RandomNumberGenerator.GetInt32(1, 101));
            // 重试保存沿用同一轮结果，不能换一个获胜者后再次发放同一件装备。
            obj.EquipmentRollPlayers = players;
            obj.EquipmentRollResult = rounds;
            PlayerSession winner = players[rounds.WinnerIndex];
            EquipData equipment = obj.EquipmentDrop.Clone();
            // 使用掉落唯一 ID，保存重试不会重复发奖。
            if (string.IsNullOrWhiteSpace(equipment.Id)) equipment.Id = Guid.NewGuid().ToString("N");
            obj.EquipmentDrop.Id = equipment.Id;
            UserData saved;
            if (!obj.EquipmentAwarded)
            {
                saved = await winner.MutateAndSaveUserDataSnapshotAsync(data =>
                {
                    if (!data.EquipData.Any(e => e.Id == equipment.Id)) data.EquipData.Add(equipment.Clone());
                });
                obj.EquipmentAwarded = true;
            }
            else saved = winner.UserDataSnapshot!.Clone();
            await winner.SendBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server, ServerMsgType = ServerMsgType.UpdateUserData,
                UserData = saved
            }.ToByteArray());
            var result = new Msg
            {
                MsgType = ProtobufMsgType.Server, ServerMsgType = ServerMsgType.RoomEquipmentRollResult,
                NetworkObjectId = objectId, Id = winner.UserId
            };
            var lines = new List<string> { "高品质装备自动掷骰子（1–100点）" };
            for (int roundIndex = 0; roundIndex < rounds.Rounds.Count; roundIndex++)
                foreach (var score in rounds.Rounds[roundIndex])
                {
                    var player = players[score.PlayerIndex];
                    result.EquipmentRollScores.Add(new EquipmentRollScoreData
                    {
                        UserId = player.UserId,
                        PlayerObjectId = player.NetworkObjectId,
                        PlayerName = player.UserDataSnapshot?.Name ?? player.UserId,
                        Round = roundIndex + 1,
                        Score = score.Score
                    });
                }
            foreach (var round in rounds.Rounds)
                lines.Add(string.Join("，", round.Select(r =>
                    $"{players[r.PlayerIndex].UserDataSnapshot?.Name ?? players[r.PlayerIndex].UserId}：{r.Score}点")));
            lines.Add($"{winner.UserDataSnapshot?.Name ?? winner.UserId} 获得装备，已加入背包");
            result.TipsSrt = string.Join("\n", lines);
            result.ChannelChatMessage = new ChannelChatMessageData
            {
                MessageId = Guid.NewGuid().ToString("N"), Channel = ChatChannelType.Room,
                RoomId = obj.RoomId, SenderName = "装备分配", Content = result.TipsSrt,
                SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            result.RewardEquip.Add(equipment);
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(result.ToByteArray(), null, obj.RoomId);
            if (RemoveObject(objectId, out _))
            {
                NetworkSyncVarManager.Instance.RemoveObject(objectId);
                await BroadcastDestroy(obj);
            }
        }
        finally
        {
            lock (obj) obj.EquipmentRollStarted = false;
        }
    }

    private static Task SendClaimResultAsync(
        PlayerSession receiver, uint objectId, ObjectClaim? winner, bool success)
    {
        return receiver.SendBinaryAsync(CreateClaimResult(objectId, winner, success).ToByteArray());
    }

    private static Msg CreateClaimResult(uint objectId, ObjectClaim? winner, bool success)
    {
        return new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomObjectClaimResult,
            NetworkObjectId = objectId,
            Id = winner?.UserId ?? string.Empty,
            RoleType = success ? 1 : 0,
            NetworkObject = winner == null ? null : new NetworkObjectData
            {
                ObjectId = winner.PlayerObjectId,
                PlayerId = winner.UserId,
                PlayerObject = true
            }
        };
    }

    public bool CanSync(PlayerSession sender, uint objectId)
    {
        return _objects.TryGetValue(objectId, out RoomObject? obj) && obj.RoomId == sender.NetworkRoomId &&
               obj.OwnerSessionId == sender.PlayerId;
    }

    public bool TryGetAuthorizedRoomId(PlayerSession sender, uint objectId, out uint roomId)
    {
        if (_objects.TryGetValue(objectId, out RoomObject? obj) && obj.RoomId == sender.NetworkRoomId)
        {
            roomId = obj.RoomId;
            return true;
        }
        roomId = 0;
        return false;
    }

    public Task<bool> RelayTransformAsync(PlayerSession sender, NetworkTransformData state)
    {
        if (!TryApplyTransform(sender, state)) return Task.FromResult(false);
        if (!PlayerSessionManager.Instance.HasRoomObserver(sender.NetworkRoomId, sender.PlayerId))
            return Task.FromResult(true);
        PlayerSessionManager.Instance.BroadcastLatestBinary(
            PlayerSessionManager.CreateRealtimeKey(ServerMsgType.NetworkTransformUpdate, state.ObjectId),
            new DeferredMessagePacket(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkTransformUpdate, NetworkTransform = state }),
            sender.PlayerId, sender.NetworkRoomId, maxAgeMs: 250);
        return Task.FromResult(true);
    }

    public bool TryApplyTransform(PlayerSession sender, NetworkTransformData state)
    {
        if (!NetworkStateValidation.IsValid(state)) return false;
        if (!_objects.TryGetValue(state.ObjectId, out RoomObject? obj) || obj.RoomId != sender.NetworkRoomId ||
            obj.OwnerSessionId != sender.PlayerId) return false;
        if (obj.Transform != null && obj.TransformOwnerSessionId == sender.PlayerId &&
            state.Sequence <= obj.Transform.Sequence) return false;
        obj.Transform = state.Clone();
        obj.TransformOwnerSessionId = sender.PlayerId;
        return true;
    }

    /// <summary>每批位置只编码一次，各接收连接保留尚未发送的最新状态。</summary>
    public void BroadcastTransformBatch(PlayerSession sender, IReadOnlyList<NetworkTransformData> snapshots)
    {
        if (snapshots.Count == 0) return;
        uint roomId = sender.NetworkRoomId;
        if (roomId == 0 || roomId == NetworkRoomManager.LobbyRoomId) return;
        if (!PlayerSessionManager.Instance.HasRoomObserver(roomId, sender.PlayerId)) return;
        var groups = new Dictionary<uint, Msg>();
        foreach (NetworkTransformData snapshot in snapshots)
        {
            uint groupId = snapshot.ObjectId >> 7;
            if (!groups.TryGetValue(groupId, out Msg? group))
            {
                group = new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkTransformUpdate };
                groups.Add(groupId, group);
            }
            group.NetworkTransforms.Add(snapshot);
        }
        var packets = groups.Select(pair => (Key: ((ulong)pair.Key << 32) | 0x40000000UL,
            Payload: new TransformBatchPacket(pair.Value.NetworkTransforms))).ToArray();
        foreach (PlayerSession receiver in PlayerSessionManager.Instance.SessionsForRoom(roomId))
        {
            if (!receiver.IsConnected || receiver.NetworkRoomId != roomId ||
                receiver.PlayerId == sender.PlayerId || string.IsNullOrWhiteSpace(receiver.UserId) ||
                receiver.ServerId != sender.ServerId) continue;
            foreach (var packet in packets)
                receiver.QueueLatestTransformBatch(packet.Key, packet.Payload);
        }
    }

    public Task<bool> RelayAnimationAsync(PlayerSession sender, NetworkAnimationData state)
    {
        if (!_objects.TryGetValue(state.ObjectId, out RoomObject? obj) || obj.RoomId != sender.NetworkRoomId ||
            obj.OwnerSessionId != sender.PlayerId) return Task.FromResult(false);
        if (!NetworkStateValidation.IsValid(state)) return Task.FromResult(false);
        var cached = state.Clone();
        var accepted = obj.Animations.AddOrUpdate(state.TrackIndex, cached,
            (_, old) => cached.Sequence > old.Sequence ? cached : old);
        if (!ReferenceEquals(accepted, cached)) return Task.FromResult(false);
        if (!PlayerSessionManager.Instance.HasRoomObserver(obj.RoomId, sender.PlayerId))
            return Task.FromResult(true);
        PlayerSessionManager.Instance.BroadcastLatestBinary(
            PlayerSessionManager.CreateRealtimeKey(ServerMsgType.NetworkAnimationUpdate, state.ObjectId, state.TrackIndex),
            DeferredMessagePacket.FromOwnedMessage(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkAnimationUpdate, NetworkAnimation = cached }),
            sender.PlayerId, obj.RoomId);
        return Task.FromResult(true);
    }

    public async Task SendRoomObjectsToAsync(PlayerSession receiver)
    {
        uint roomId = receiver.NetworkRoomId;
        if (roomId == 0 || roomId == NetworkRoomManager.LobbyRoomId) return;
        uint sceneEpoch = NetworkRoomManager.Instance.GetSceneEpoch(roomId);
        var batch = new List<RoomObject>(InitialObjectsPerPacket);
        var sent = new List<RoomObject>();
        foreach (RoomObject obj in RoomObjects(roomId))
        {
            if (obj.RoomId != roomId) continue;
            batch.Add(obj);
            sent.Add(obj);
            if (batch.Count == InitialObjectsPerPacket)
            {
                if (!await SendInitialObjectBatchAsync(receiver, batch, roomId, sceneEpoch)) return;
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            if (!await SendInitialObjectBatchAsync(receiver, batch, roomId, sceneEpoch)) return;

        // 销毁与晚加入快照可能交错。可靠队列中补一条销毁，防止先销毁后生成留下幽灵对象。
        foreach (RoomObject obj in sent)
        {
            if (!IsCurrentSnapshotReceiver(receiver, roomId, sceneEpoch)) return;
            if (_objects.TryGetValue(obj.ObjectId, out RoomObject? current) &&
                ReferenceEquals(current, obj)) continue;
            await receiver.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkObjectDespawn,
                NetworkObject = new NetworkObjectData { ObjectId = obj.ObjectId,
                    RoomId = roomId, AiObject = obj.IsAi } }.ToByteArray());
        }
    }

    private static async Task<bool> SendInitialObjectBatchAsync(PlayerSession receiver,
        List<RoomObject> batch, uint roomId, uint sceneEpoch)
    {
        // 一条可靠消息携带多个生成对象，初始位置用 ObjectId 关联；
        // 状态更新仍排在生成包之后，客户端异步加载期间可先缓存。
        await receiver.WaitForSnapshotSendCapacityAsync();
        if (!IsCurrentSnapshotReceiver(receiver, roomId, sceneEpoch)) return false;
        var spawns = new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectSpawn };
        foreach (RoomObject obj in batch)
        {
            spawns.NetworkObjects.Add(ToNetworkData(obj));
            if (obj.Transform != null)
                spawns.NetworkTransforms.Add(obj.Transform.Clone());
        }
        await receiver.SendBinaryAsync(spawns.ToByteArray());
        var animations = new List<NetworkAnimationData>();
        var syncVars = new List<NetworkSyncVarData>();
        foreach (RoomObject obj in batch)
        {
            foreach (NetworkAnimationData animation in obj.Animations.Values)
                animations.Add(animation.Clone());
            NetworkSyncVarManager.Instance.AppendObjectStates(receiver.NetworkRoomId,
                obj.ObjectId, syncVars);
        }
        return await SendInitialStateBatchesAsync(receiver, animations, syncVars,
            roomId, sceneEpoch);
    }

    private static async Task<bool> SendInitialStateBatchesAsync(PlayerSession receiver,
        List<NetworkAnimationData> animations, List<NetworkSyncVarData> syncVars,
        uint roomId, uint sceneEpoch)
    {
        var state = new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectStateBatch };
        int estimatedBytes = 8;
        foreach (NetworkAnimationData animation in animations)
        {
            int itemBytes = animation.CalculateSize() + 8;
            if (estimatedBytes + itemBytes > InitialStatePacketBytes &&
                state.NetworkAnimations.Count + state.NetworkSyncVars.Count > 0)
            {
                await receiver.WaitForSnapshotSendCapacityAsync();
                if (!IsCurrentSnapshotReceiver(receiver, roomId, sceneEpoch)) return false;
                await receiver.SendBinaryAsync(state.ToByteArray());
                state = new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkObjectStateBatch };
                estimatedBytes = 8;
            }
            state.NetworkAnimations.Add(animation);
            estimatedBytes += itemBytes;
        }
        foreach (NetworkSyncVarData syncVar in syncVars)
        {
            int itemBytes = syncVar.CalculateSize() + 8;
            if (estimatedBytes + itemBytes > InitialStatePacketBytes &&
                state.NetworkAnimations.Count + state.NetworkSyncVars.Count > 0)
            {
                await receiver.WaitForSnapshotSendCapacityAsync();
                if (!IsCurrentSnapshotReceiver(receiver, roomId, sceneEpoch)) return false;
                await receiver.SendBinaryAsync(state.ToByteArray());
                state = new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkObjectStateBatch };
                estimatedBytes = 8;
            }
            state.NetworkSyncVars.Add(syncVar);
            estimatedBytes += itemBytes;
        }
        if (state.NetworkAnimations.Count + state.NetworkSyncVars.Count > 0)
        {
            await receiver.WaitForSnapshotSendCapacityAsync();
            if (!IsCurrentSnapshotReceiver(receiver, roomId, sceneEpoch)) return false;
            await receiver.SendBinaryAsync(state.ToByteArray());
        }
        return true;
    }

    private static bool IsCurrentSnapshotReceiver(PlayerSession receiver,
        uint roomId, uint sceneEpoch) => receiver.IsConnected &&
        receiver.NetworkRoomId == roomId &&
        NetworkRoomManager.Instance.GetSceneEpoch(roomId) == sceneEpoch;

    public async Task HandleOwnerLeavingAsync(PlayerSession owner, uint roomId)
    {
        PlayerSession[] candidates = PlayerSessionManager.Instance.GetRoomSessions(roomId)
            .Where(session => session != owner).ToArray();
        AiOwnerLoad load = GetAiOwnerLoad(roomId);
        foreach (RoomObject obj in RoomObjects(roomId))
        {
            if (obj.RoomId != roomId || obj.OwnerSessionId != owner.PlayerId) continue;
            if (!obj.IsAi)
            {
                if (RemoveObject(obj.ObjectId, out _))
                {
                    _claims.TryRemove(obj.ObjectId, out _);
                    NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
                    await BroadcastDestroy(obj);
                }
                continue;
            }

            PlayerSession? newOwner;
            lock (load.Gate)
            {
                if (!_objects.TryGetValue(obj.ObjectId, out RoomObject? current) ||
                    !ReferenceEquals(current, obj) || obj.OwnerSessionId != owner.PlayerId)
                    continue;
                newOwner = SelectLeastLoadedAiOwner(candidates, load.Counts, roomId);
                if (newOwner != null)
                {
                    AdjustAiOwnerCount(load.Counts, obj.OwnerSessionId, -1);
                    AdjustAiOwnerCount(load.Counts, newOwner.PlayerId, 1);
                    obj.OwnerSessionId = newOwner.PlayerId;
                    obj.Animations.Clear();
                }
            }
            if (newOwner == null)
            {
                if (RemoveObject(obj.ObjectId, out _))
                {
                    RemoveAiOwnerLoad(obj);
                    _claims.TryRemove(obj.ObjectId, out _);
                    NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
                    await BroadcastDestroy(obj);
                }
                continue;
            }

            NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkObjectAuthorityChanged,
                NetworkObject = ToNetworkData(obj) }.ToByteArray(), null, roomId);
        }
    }

    private Dictionary<string, int> CountOwnedAiInRoom(uint roomId)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (RoomObject obj in RoomObjects(roomId))
            if (obj.IsAi && obj.RoomId == roomId)
                counts[obj.OwnerSessionId] = counts.GetValueOrDefault(obj.OwnerSessionId) + 1;
        return counts;
    }

    private AiOwnerLoad GetAiOwnerLoad(uint roomId) => _aiOwnerLoads.GetOrAdd(roomId,
        id => new AiOwnerLoad(CountOwnedAiInRoom(id)));

    private void RemoveAiOwnerLoad(RoomObject obj)
    {
        if (!obj.IsAi || !_aiOwnerLoads.TryGetValue(obj.RoomId, out AiOwnerLoad? load)) return;
        lock (load.Gate)
            AdjustAiOwnerCount(load.Counts, obj.OwnerSessionId, -1);
    }

    private static void AdjustAiOwnerCount(Dictionary<string, int> counts,
        string ownerId, int delta)
    {
        int next = counts.GetValueOrDefault(ownerId) + delta;
        if (next > 0) counts[ownerId] = next;
        else counts.Remove(ownerId);
    }

    private static PlayerSession? SelectLeastLoadedAiOwner(
        PlayerSession[] candidates, Dictionary<string, int> counts, uint roomId)
    {
        PlayerSession? best = null;
        int bestCount = int.MaxValue;
        foreach (PlayerSession candidate in candidates)
        {
            if (!candidate.IsConnected || candidate.NetworkRoomId != roomId) continue;
            int count = counts.GetValueOrDefault(candidate.PlayerId);
            if (best != null && (count > bestCount ||
                count == bestCount && StringComparer.Ordinal.Compare(candidate.PlayerId, best.PlayerId) >= 0))
                continue;
            best = candidate;
            bestCount = count;
        }
        return best;
    }

    private static NetworkObjectData ToNetworkData(RoomObject obj, string spawnRequestId = "")
    {
        return new NetworkObjectData { ObjectId = obj.ObjectId, RoomId = obj.RoomId,
            PrefabId = obj.PrefabId, RoleType = obj.RoleType,
            MonsterBoxKey = obj.MonsterBoxKey, BoxIndex = obj.BoxIndex,
            TriggerId = obj.TriggerId,
            OwnerPlayerId = PlayerSessionManager.Instance.GetSession(obj.OwnerSessionId)?.UserId ?? string.Empty,
            PlayerObject = false, AiObject = obj.IsAi, SpawnRequestId = spawnRequestId,
            EquipmentDrop = obj.EquipmentDrop?.Clone(), EquipmentRollRequired = obj.EquipmentRollRequired };
    }

    public async Task DestroyRoomObjectsAsync(uint roomId, bool notifyClients = true)
    {
        foreach (RoomObject obj in RoomObjects(roomId))
            if (obj.RoomId == roomId && RemoveObject(obj.ObjectId, out _))
            {
                RemoveAiOwnerLoad(obj);
                _claims.TryRemove(obj.ObjectId, out _);
                NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
                if (notifyClients) await BroadcastDestroy(obj);
            }
        _triggeredAiSpawns.TryRemove(roomId, out _);
        _aiOwnerLoads.TryRemove(roomId, out _);
    }

    private static Task BroadcastDestroy(RoomObject obj)
    {
        return PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectDespawn,
            NetworkObject = new NetworkObjectData { ObjectId = obj.ObjectId, RoomId = obj.RoomId,
                PrefabId = obj.PrefabId, RoleType = obj.RoleType,
                PlayerObject = false, AiObject = obj.IsAi } }.ToByteArray(), null, obj.RoomId);
    }
}
