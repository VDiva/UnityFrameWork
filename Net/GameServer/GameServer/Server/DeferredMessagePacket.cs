using GameData;
using Google.Protobuf;
namespace WebSocketDemo;

/// <summary>不可变的单状态消息；覆盖时只替换引用，所有接收者共享首次发送的编码。</summary>
public sealed class DeferredMessagePacket : ISessionPacketPayload
{
    private readonly Lazy<byte[]> _bytes;
    public int ByteCount { get; }
    public bool IsEncoded => _bytes.IsValueCreated;
    public DeferredMessagePacket(Msg message) : this(message.Clone(), true) { }
    private DeferredMessagePacket(Msg owned, bool _) 
    {
        ByteCount = owned.CalculateSize();
        _bytes = new Lazy<byte[]>(owned.ToByteArray, LazyThreadSafetyMode.ExecutionAndPublication);
    }
    // 调用方转移所有权，之后不得修改消息及其子消息。
    internal static DeferredMessagePacket FromOwnedMessage(Msg owned) => new(owned, true);
    public byte[] GetBytes() => _bytes.Value;
    public ISessionPacketPayload MergeFrom(ISessionPacketPayload previous) => this;
}
