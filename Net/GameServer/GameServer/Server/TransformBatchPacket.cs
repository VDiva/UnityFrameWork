using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>广播共享不可变快照，发送时才编码；支持标准 Protobuf 与位压缩 CompactTransformCodec 双编码。</summary>
public sealed class TransformBatchPacket : ISessionPacketPayload
{
    private readonly List<NetworkTransformData> _snapshots;
    private readonly bool _compact;
    private readonly Lazy<byte[]> _bytes;
    private int _byteCount = -1;

    public int ByteCount => _byteCount >= 0 ? _byteCount : (_byteCount = _bytes.Value.Length);

    public TransformBatchPacket(IEnumerable<NetworkTransformData> snapshots, bool compact = false)
        : this(CloneList(snapshots), compact) { }

    private TransformBatchPacket(List<NetworkTransformData> ownedSnapshots, bool compact)
    {
        _snapshots = ownedSnapshots;
        _compact = compact;
        _bytes = new Lazy<byte[]>(() => BuildWireBytes(_snapshots, _compact), LazyThreadSafetyMode.ExecutionAndPublication);
        if (!compact)
        {
            var msg = new Msg { MsgType = ProtobufMsgType.Server, ServerMsgType = ServerMsgType.NetworkTransformUpdate };
            msg.NetworkTransforms.AddRange(_snapshots);
            _byteCount = msg.CalculateSize();
        }
    }

    private static List<NetworkTransformData> CloneList(IEnumerable<NetworkTransformData> source)
    {
        var list = source is ICollection<NetworkTransformData> col
            ? new List<NetworkTransformData>(col.Count)
            : new List<NetworkTransformData>();
        foreach (var s in source)
            list.Add(s.Clone());
        return list;
    }

    private static byte[] BuildWireBytes(List<NetworkTransformData> snapshots, bool compact)
    {
        var message = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkTransformUpdate
        };
        if (compact)
            message.CompactTransforms = CompactTransformCodec.EncodeBatch(snapshots);
        else
            message.NetworkTransforms.AddRange(snapshots);
        return message.ToByteArray();
    }

    public byte[] GetBytes() => _bytes.Value;

    public ISessionPacketPayload MergeFrom(ISessionPacketPayload previous)
    {
        List<NetworkTransformData> merged;
        if (previous is TransformBatchPacket prevBatch)
        {
            merged = new List<NetworkTransformData>(prevBatch._snapshots.Count + _snapshots.Count);
            merged.AddRange(prevBatch._snapshots);
        }
        else
        {
            Msg oldMsg = Msg.Parser.ParseFrom(previous.GetBytes());
            merged = new List<NetworkTransformData>(oldMsg.NetworkTransforms.Count + _snapshots.Count);
            if (oldMsg.CompactTransforms.Length > 0)
                CompactTransformCodec.TryDecode(oldMsg.CompactTransforms, merged);
            merged.AddRange(oldMsg.NetworkTransforms);
        }

        var indexes = new Dictionary<uint, int>(merged.Count);
        for (int i = 0; i < merged.Count; i++)
            indexes[merged[i].ObjectId] = i;

        for (int i = 0; i < _snapshots.Count; i++)
        {
            NetworkTransformData s = _snapshots[i];
            if (indexes.TryGetValue(s.ObjectId, out int idx))
                merged[idx] = s;
            else
            {
                indexes[s.ObjectId] = merged.Count;
                merged.Add(s);
            }
        }

        return new TransformBatchPacket(merged, _compact);
    }
}
