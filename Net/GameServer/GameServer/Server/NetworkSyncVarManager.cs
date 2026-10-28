using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>保存和转发通用同步变量；变量内容对服务端保持不透明。</summary>
public sealed class NetworkSyncVarManager
{
    private const int MaxValueBytes = 64 * 1024;
    public const int MaxFieldsPerObject = 256;
    public const int MaxObjectBytes = 256 * 1024;
    private sealed class ObjectStates
    {
        public readonly object Gate = new();
        public readonly Dictionary<SyncKey, SyncState> Values = new();
        public int Bytes;
        public SyncState[] Snapshot() { lock (Gate) return Values.Values.ToArray(); }
    }
    private readonly ConcurrentDictionary<uint, ObjectStates> _statesByObject = new();
    public static NetworkSyncVarManager Instance { get; } = new();

    private readonly record struct SyncKey(uint BehaviourId, uint FieldId);
    private sealed record SyncState(uint RoomId, NetworkSyncVarData Data, int Bytes);

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
        ObjectStates objectStates = _statesByObject.GetOrAdd(state.ObjectId, _ => new ObjectStates());
        lock (objectStates.Gate)
        {
            bool exists = objectStates.Values.TryGetValue(key, out var current);
            if (exists && state.Sequence <= current!.Data.Sequence) return;
            if (!exists && objectStates.Values.Count >= MaxFieldsPerObject) return;
            int bytes = state.CalculateSize();
            int nextBytes = objectStates.Bytes - (current?.Bytes ?? 0) + bytes;
            if (nextBytes > MaxObjectBytes) return;
            objectStates.Values[key] = new SyncState(roomId, state, bytes);
            objectStates.Bytes = nextBytes;
        }
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
        uint roomId = receiver.NetworkRoomId;
        uint epoch = NetworkRoomManager.Instance.GetSceneEpoch(roomId);
        var packet = NewStateBatch();
        int estimatedBytes = 8;
        async Task<bool> Flush()
        {
            await receiver.WaitForSnapshotSendCapacityAsync();
            if (!receiver.IsConnected || receiver.NetworkRoomId != roomId ||
                NetworkRoomManager.Instance.GetSceneEpoch(roomId) != epoch) return false;
            await receiver.SendBinaryAsync(packet.ToByteArray());
            return true;
        }
        foreach (SyncState state in objectStates.Snapshot())
        {
            if (state.RoomId != roomId) continue;
            int bytes = state.Bytes + 8;
            if (estimatedBytes + bytes > 48 * 1024 && packet.NetworkSyncVars.Count > 0)
            {
                if (!await Flush()) return;
                packet = NewStateBatch();
                estimatedBytes = 8;
            }
            // 缓存持有私有克隆，发送快照只读引用即可，不再复制每个字段。
            packet.NetworkSyncVars.Add(state.Data);
            estimatedBytes += bytes;
        }
        if (packet.NetworkSyncVars.Count > 0) await Flush();
    }

    private static Msg NewStateBatch() => new() { MsgType = ProtobufMsgType.Server,
        ServerMsgType = ServerMsgType.NetworkObjectStateBatch };

    public void AppendObjectStates(uint roomId, uint objectId, List<NetworkSyncVarData> destination)
    {
        if (!_statesByObject.TryGetValue(objectId, out var objectStates)) return;
        foreach (SyncState state in objectStates.Snapshot())
            if (state.RoomId == roomId)
                destination.Add(state.Data.Clone());
    }

    public void RemoveObject(uint objectId)
    {
        _statesByObject.TryRemove(objectId, out _);
    }
}
