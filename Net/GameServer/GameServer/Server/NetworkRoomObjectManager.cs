using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>管理非玩家房间网络物体，ID、房间和所有权均以服务端记录为准。</summary>
public sealed class NetworkRoomObjectManager
{
    private const int MaxPrefabIdLength = 128;
    private const int MaxTriggerIdLength = 128;
    private const int DropRoleType = 5;
    private readonly ConcurrentDictionary<uint, RoomObject> _objects = new();
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
        public int BoxIndex;
        public int RoleType;
        public bool IsAi;
        public NetworkTransformData? Transform;
        public ConcurrentDictionary<int, NetworkAnimationData> Animations = new();
    }

    private sealed record ObjectClaim(string SessionId, string UserId, uint PlayerObjectId);

    public async Task SpawnAsync(PlayerSession owner, NetworkObjectData? request, NetworkTransformData? transform)
    {
        if (owner.NetworkRoomId == 0 || owner.NetworkRoomId == NetworkRoomManager.LobbyRoomId ||
            request == null || string.IsNullOrWhiteSpace(request.PrefabId) || request.PrefabId.Length > MaxPrefabIdLength)
            return;

        uint objectId = PlayerSessionManager.Instance.AllocateNetworkObjectId();
        var roomObject = new RoomObject { ObjectId = objectId, RoomId = owner.NetworkRoomId,
            PrefabId = request.PrefabId.Trim(), OwnerSessionId = owner.PlayerId,
            RoleType = request.RoleType, IsAi = false };
        if (!_objects.TryAdd(objectId, roomObject)) return;

        var objectData = new NetworkObjectData { ObjectId = objectId, RoomId = roomObject.RoomId,
            PrefabId = roomObject.PrefabId, OwnerPlayerId = owner.UserId ?? string.Empty,
            PlayerObject = false, AiObject = false, RoleType = roomObject.RoleType,
            SpawnRequestId = request.SpawnRequestId };
        NetworkTransformData? initialTransform = null;
        if (transform != null)
        {
            initialTransform = transform.Clone();
            initialTransform.ObjectId = objectId;
            roomObject.Transform = initialTransform.Clone();
        }
        Console.WriteLine(
            $"[NetworkSpawn] prefabId={objectData.PrefabId}, objectId={objectData.ObjectId}, " +
            $"requestId={objectData.SpawnRequestId}, owner={objectData.OwnerPlayerId}");
        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectSpawn, NetworkObject = objectData,
            NetworkTransform = initialTransform }.ToByteArray(), null, roomObject.RoomId);

        if (initialTransform != null)
        {
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                NetworkTransform = initialTransform }.ToByteArray(), null, roomObject.RoomId);
        }
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

        PlayerSession? owner = PlayerSessionManager.Instance.GetRoomSessions(roomId)
            .OrderBy(GetOwnedAiCount)
            .ThenBy(s => s.PlayerId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (owner == null) return false;

        uint objectId = PlayerSessionManager.Instance.AllocateNetworkObjectId();
        var obj = new RoomObject { ObjectId = objectId, RoomId = roomId,
            PrefabId = request.PrefabId.Trim(), OwnerSessionId = owner.PlayerId,
            MonsterBoxKey = request.MonsterBoxKey, BoxIndex = request.BoxIndex,
            RoleType = request.RoleType, IsAi = true };
        if (!_objects.TryAdd(objectId, obj)) return false;

        NetworkTransformData? initialTransform = null;
        if (transform != null)
        {
            initialTransform = transform.Clone();
            initialTransform.ObjectId = objectId;
            obj.Transform = initialTransform.Clone();
        }

        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkObjectSpawn,
            NetworkObject = ToNetworkData(obj, request.SpawnRequestId),
            NetworkTransform = initialTransform }.ToByteArray(), null, obj.RoomId);
        if (initialTransform != null)
        {
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                NetworkTransform = initialTransform }.ToByteArray(), null, obj.RoomId);
        }
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
        if (_objects.TryRemove(objectId, out _))
        {
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
            obj.RoomId != requester.NetworkRoomId || obj.IsAi || obj.RoleType != DropRoleType)
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
        if (!_objects.TryGetValue(state.ObjectId, out RoomObject? obj) || obj.RoomId != sender.NetworkRoomId ||
            obj.OwnerSessionId != sender.PlayerId) return Task.FromResult(false);
        obj.Transform = state.Clone();
        PlayerSessionManager.Instance.BroadcastLatestBinary(
            PlayerSessionManager.CreateRealtimeKey(ServerMsgType.NetworkTransformUpdate, state.ObjectId),
            new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkTransformUpdate, NetworkTransform = state }.ToByteArray(),
            sender.PlayerId, obj.RoomId);
        return Task.FromResult(true);
    }

    public Task<bool> RelayAnimationAsync(PlayerSession sender, NetworkAnimationData state)
    {
        if (!_objects.TryGetValue(state.ObjectId, out RoomObject? obj) || obj.RoomId != sender.NetworkRoomId ||
            obj.OwnerSessionId != sender.PlayerId) return Task.FromResult(false);
        obj.Animations[state.TrackIndex] = state.Clone();
        PlayerSessionManager.Instance.BroadcastLatestBinary(
            PlayerSessionManager.CreateRealtimeKey(ServerMsgType.NetworkAnimationUpdate, state.ObjectId, state.TrackIndex),
            new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkAnimationUpdate, NetworkAnimation = state }.ToByteArray(),
            sender.PlayerId, obj.RoomId);
        return Task.FromResult(true);
    }

    public async Task SendRoomObjectsToAsync(PlayerSession receiver)
    {
        foreach (RoomObject obj in _objects.Values)
        {
            if (obj.RoomId != receiver.NetworkRoomId) continue;
            await receiver.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkObjectSpawn,
                NetworkObject = new NetworkObjectData { ObjectId = obj.ObjectId, RoomId = obj.RoomId,
                    PrefabId = obj.PrefabId,
                    RoleType = obj.RoleType,
                    OwnerPlayerId = PlayerSessionManager.Instance.GetSession(obj.OwnerSessionId)?.UserId ?? string.Empty,
                    PlayerObject = false, AiObject = obj.IsAi } }.ToByteArray());
            if (obj.Transform != null)
                await receiver.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                    NetworkTransform = obj.Transform.Clone() }.ToByteArray());
            foreach (NetworkAnimationData animation in obj.Animations.Values)
                await receiver.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkAnimationUpdate,
                    NetworkAnimation = animation.Clone() }.ToByteArray());
            await NetworkSyncVarManager.Instance.SendObjectStateAsync(receiver, obj.ObjectId);
        }
    }

    public async Task HandleOwnerLeavingAsync(PlayerSession owner, uint roomId)
    {
        foreach (RoomObject obj in _objects.Values)
        {
            if (obj.RoomId != roomId || obj.OwnerSessionId != owner.PlayerId) continue;
            if (!obj.IsAi)
            {
                if (_objects.TryRemove(obj.ObjectId, out _))
                {
                    _claims.TryRemove(obj.ObjectId, out _);
                    NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
                    await BroadcastDestroy(obj);
                }
                continue;
            }

            PlayerSession? newOwner = PlayerSessionManager.Instance.GetRoomSessions(roomId)
                .Where(s => s != owner)
                .OrderBy(GetOwnedAiCount)
                .ThenBy(s => s.PlayerId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (newOwner == null)
            {
                if (_objects.TryRemove(obj.ObjectId, out _))
                {
                    _claims.TryRemove(obj.ObjectId, out _);
                    NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
                    await BroadcastDestroy(obj);
                }
                continue;
            }

            obj.OwnerSessionId = newOwner.PlayerId;
            NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkObjectAuthorityChanged,
                NetworkObject = ToNetworkData(obj) }.ToByteArray(), null, roomId);
        }
    }

    private int GetOwnedAiCount(PlayerSession session)
    {
        return _objects.Values.Count(o => o.IsAi && o.OwnerSessionId == session.PlayerId);
    }

    private static NetworkObjectData ToNetworkData(RoomObject obj, string spawnRequestId = "")
    {
        return new NetworkObjectData { ObjectId = obj.ObjectId, RoomId = obj.RoomId,
            PrefabId = obj.PrefabId, RoleType = obj.RoleType,
            MonsterBoxKey = obj.MonsterBoxKey, BoxIndex = obj.BoxIndex,
            OwnerPlayerId = PlayerSessionManager.Instance.GetSession(obj.OwnerSessionId)?.UserId ?? string.Empty,
            PlayerObject = false, AiObject = obj.IsAi, SpawnRequestId = spawnRequestId };
    }

    public async Task DestroyRoomObjectsAsync(uint roomId, bool notifyClients = true)
    {
        foreach (RoomObject obj in _objects.Values)
            if (obj.RoomId == roomId && _objects.TryRemove(obj.ObjectId, out _))
            {
                _claims.TryRemove(obj.ObjectId, out _);
                NetworkSyncVarManager.Instance.RemoveObject(obj.ObjectId);
                if (notifyClients) await BroadcastDestroy(obj);
            }
        _triggeredAiSpawns.TryRemove(roomId, out _);
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
