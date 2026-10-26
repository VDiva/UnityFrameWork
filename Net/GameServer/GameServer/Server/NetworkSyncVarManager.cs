using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>保存和转发通用同步变量；变量内容对服务端保持不透明。</summary>
public sealed class NetworkSyncVarManager
{
    private const int MaxValueBytes = 64 * 1024;
    private readonly ConcurrentDictionary<uint, ConcurrentDictionary<SyncKey, SyncState>> _statesByObject = new();
    public static NetworkSyncVarManager Instance { get; } = new();

    private readonly record struct SyncKey(uint BehaviourId, uint FieldId);
    private sealed record SyncState(uint RoomId, NetworkSyncVarData Data);

    public async Task RelayAsync(PlayerSession sender, NetworkSyncVarData? input)
    {
        if (input == null || input.ObjectId == 0 || input.Value.Length > MaxValueBytes || sender.NetworkRoomId == 0)
            return;

        uint roomId;
        if (input.ObjectId == sender.NetworkObjectId)
            roomId = sender.NetworkRoomId;
        else
        {
            if (!NetworkRoomObjectManager.Instance.CanSync(sender, input.ObjectId)) return;
            roomId = sender.NetworkRoomId;
        }

        NetworkSyncVarData state = input.Clone();
        var key = new SyncKey(state.BehaviourId, state.FieldId);
        ConcurrentDictionary<SyncKey, SyncState> objectStates = _statesByObject.GetOrAdd(
            state.ObjectId, _ => new ConcurrentDictionary<SyncKey, SyncState>());
        var next = new SyncState(roomId, state.Clone());
        SyncState accepted = objectStates.AddOrUpdate(key, next,
            (_, current) => state.Sequence > current.Data.Sequence ? next : current);
        if (!ReferenceEquals(accepted, next)) return;
        if (!PlayerSessionManager.Instance.HasRoomObserver(roomId, sender.PlayerId)) return;
        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkSyncVarUpdate,
            NetworkSyncVar = state
        }.ToByteArray(), sender.PlayerId, roomId);
    }

    public async Task SendObjectStateAsync(PlayerSession receiver, uint objectId)
    {
        if (!_statesByObject.TryGetValue(objectId, out var objectStates)) return;
        foreach (SyncState state in objectStates.Values)
        {
            if (state.RoomId != receiver.NetworkRoomId) continue;
            await receiver.SendBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkSyncVarUpdate,
                NetworkSyncVar = state.Data.Clone()
            }.ToByteArray());
        }
    }

    public void AppendObjectStates(uint roomId, uint objectId, List<NetworkSyncVarData> destination)
    {
        if (!_statesByObject.TryGetValue(objectId, out var objectStates)) return;
        foreach (SyncState state in objectStates.Values)
            if (state.RoomId == roomId)
                destination.Add(state.Data.Clone());
    }

    public void RemoveObject(uint objectId)
    {
        _statesByObject.TryRemove(objectId, out _);
    }
}
