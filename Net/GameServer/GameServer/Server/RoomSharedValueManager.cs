using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>服务端权威的房间共享整数。客户端只能提交增量，最终值由服务端原子计算并广播。</summary>
public sealed class RoomSharedValueManager
{
    private const int MaxKeyLength = 64;
    private const long MaxDelta = 1_000_000_000;
    private readonly ConcurrentDictionary<StateKey, State> _states = new();

    public static RoomSharedValueManager Instance { get; } = new();

    private readonly record struct StateKey(string ServerId, uint RoomId, string Key);
    private readonly record struct State(long Value, ulong Version);

    public async Task AddAsync(PlayerSession sender, RoomSharedValueData? request)
    {
        if (request == null || sender.NetworkRoomId == 0 || string.IsNullOrWhiteSpace(sender.UserId))
            return;

        string key = request.Key.Trim();
        if (key.Length == 0 || key.Length > MaxKeyLength || request.Delta == 0 ||
            request.Delta < -MaxDelta || request.Delta > MaxDelta)
            return;

        string serverId = ServerScope.Normalize(sender.ServerId);
        uint roomId = sender.NetworkRoomId;
        var stateKey = new StateKey(serverId, roomId, key);
        State updated = _states.AddOrUpdate(
            stateKey,
            _ => new State(request.Delta, 1),
            (_, old) => new State(checked(old.Value + request.Delta), checked(old.Version + 1)));

        await BroadcastAsync(serverId, roomId, key, updated);
    }

    public async Task SetAsync(PlayerSession sender, RoomSharedValueData? request)
    {
        if (request == null || sender.NetworkRoomId == 0 || string.IsNullOrWhiteSpace(sender.UserId))
            return;

        string key = request.Key.Trim();
        if (key.Length == 0 || key.Length > MaxKeyLength ||
            request.Value < -MaxDelta || request.Value > MaxDelta)
            return;

        string serverId = ServerScope.Normalize(sender.ServerId);
        uint roomId = sender.NetworkRoomId;
        var stateKey = new StateKey(serverId, roomId, key);
        State updated = _states.AddOrUpdate(
            stateKey,
            _ => new State(request.Value, 1),
            (_, old) => new State(request.Value, checked(old.Version + 1)));

        await BroadcastAsync(serverId, roomId, key, updated);
    }

    public async Task SendSnapshotAsync(PlayerSession receiver)
    {
        if (receiver.NetworkRoomId == 0) return;
        string serverId = ServerScope.Normalize(receiver.ServerId);
        uint roomId = receiver.NetworkRoomId;

        foreach ((StateKey key, State state) in _states)
        {
            if (key.RoomId != roomId || key.ServerId != serverId) continue;
            await receiver.SendBinaryAsync(CreateUpdate(key.Key, state).ToByteArray());
        }
    }

    /// <summary>房间成员可以把当前房间已经存在的全部共享值重置为零。</summary>
    public async Task ResetAllAsync(PlayerSession sender)
    {
        if (sender.NetworkRoomId == 0 || string.IsNullOrWhiteSpace(sender.UserId))
            return;

        string serverId = ServerScope.Normalize(sender.ServerId);
        uint roomId = sender.NetworkRoomId;
        var updates = new List<(string Key, State State)>();

        foreach (StateKey key in _states.Keys)
        {
            if (key.RoomId != roomId || key.ServerId != serverId) continue;

            if (_states.TryRemove(key, out State old))
                updates.Add((key.Key, new State(0, checked(old.Version + 1))));
        }

        foreach ((string key, State state) in updates)
            await BroadcastAsync(serverId, roomId, key, state);

        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.RoomSharedValuesReset
        }.ToByteArray(), null, roomId, serverId);
    }

    public void RemoveRoom(string? serverId, uint roomId)
    {
        string normalizedServerId = ServerScope.Normalize(serverId);
        foreach (StateKey key in _states.Keys)
            if (key.RoomId == roomId && key.ServerId == normalizedServerId)
                _states.TryRemove(key, out _);
    }

    private static Task BroadcastAsync(string serverId, uint roomId, string key, State state)
    {
        return PlayerSessionManager.Instance.BroadcastBinaryAsync(
            CreateUpdate(key, state).ToByteArray(), null, roomId, serverId);
    }

    private static Msg CreateUpdate(string key, State state)
    {
        return new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.RoomSharedValueUpdate,
            RoomSharedValue = new RoomSharedValueData
            {
                Key = key,
                Value = state.Value,
                Version = state.Version
            }
        };
    }

}
