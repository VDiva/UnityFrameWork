using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>保存和转发通用同步变量；变量内容对服务端保持不透明。</summary>
public sealed class NetworkSyncVarManager
{
    private const int MaxValueBytes = 64 * 1024;
    private readonly ConcurrentDictionary<SyncKey, SyncState> _states = new();
    public static NetworkSyncVarManager Instance { get; } = new();

    private readonly record struct SyncKey(uint ObjectId, uint BehaviourId, uint FieldId);
    private sealed record SyncState(uint RoomId, NetworkSyncVarData Data);

    public async Task RelayAsync(PlayerSession sender, NetworkSyncVarData? input)
    {
        if (input == null || input.ObjectId == 0 || input.Value.Length > MaxValueBytes || sender.NetworkRoomId == 0)
            return;

        uint roomId;
        if (input.ObjectId == sender.NetworkObjectId)
            roomId = sender.NetworkRoomId;
        else if (!NetworkRoomObjectManager.Instance.TryGetAuthorizedRoomId(sender, input.ObjectId, out roomId))
            return;

        NetworkSyncVarData state = input.Clone();
        var key = new SyncKey(state.ObjectId, state.BehaviourId, state.FieldId);
        if (_states.TryGetValue(key, out SyncState? oldState) && state.Sequence <= oldState.Data.Sequence)
            return;

        _states[key] = new SyncState(roomId, state.Clone());
        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkSyncVarUpdate,
            NetworkSyncVar = state
        }.ToByteArray(), sender.PlayerId, roomId);
    }

    public async Task SendObjectStateAsync(PlayerSession receiver, uint objectId)
    {
        foreach (SyncState state in _states.Values)
        {
            if (state.RoomId != receiver.NetworkRoomId || state.Data.ObjectId != objectId) continue;
            await receiver.SendBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkSyncVarUpdate,
                NetworkSyncVar = state.Data.Clone()
            }.ToByteArray());
        }
    }

    public void RemoveObject(uint objectId)
    {
        foreach (SyncKey key in _states.Keys)
            if (key.ObjectId == objectId)
                _states.TryRemove(key, out _);
    }
}
