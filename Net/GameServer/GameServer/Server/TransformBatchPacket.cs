using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>广播共享不可变快照，发送时才编码；积压合并直接操作状态，避免反复解析 protobuf。</summary>
public sealed class TransformBatchPacket : ISessionPacketPayload
{
    private readonly Msg _message;
    private readonly Lazy<byte[]> _bytes;
    public int ByteCount { get; }

    public TransformBatchPacket(IEnumerable<NetworkTransformData> snapshots)
        : this(CreateMessage(snapshots)) { }

    private TransformBatchPacket(Msg message)
    {
        _message = message;
        ByteCount = message.CalculateSize();
        _bytes = new Lazy<byte[]>(message.ToByteArray, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static Msg CreateMessage(IEnumerable<NetworkTransformData> snapshots)
    {
        var message = new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkTransformUpdate };
        foreach (var snapshot in snapshots) message.NetworkTransforms.Add(snapshot.Clone());
        return message;
    }

    public byte[] GetBytes() => _bytes.Value;

    public ISessionPacketPayload MergeFrom(ISessionPacketPayload previous)
    {
        Msg old = previous is TransformBatchPacket batch ? batch._message : Msg.Parser.ParseFrom(previous.GetBytes());
        var merged = new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkTransformUpdate };
        var indexes = new Dictionary<uint, int>(old.NetworkTransforms.Count);
        foreach (var snapshot in old.NetworkTransforms)
        {
            indexes[snapshot.ObjectId] = merged.NetworkTransforms.Count;
            merged.NetworkTransforms.Add(snapshot);
        }
        foreach (var snapshot in _message.NetworkTransforms)
        {
            if (indexes.TryGetValue(snapshot.ObjectId, out int index))
                merged.NetworkTransforms[index] = snapshot;
            else
            {
                indexes.Add(snapshot.ObjectId, merged.NetworkTransforms.Count);
                merged.NetworkTransforms.Add(snapshot);
            }
        }
        return new TransformBatchPacket(merged);
    }
}
