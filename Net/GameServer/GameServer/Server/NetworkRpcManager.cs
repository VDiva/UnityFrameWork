using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>校验并转发网络对象 RPC；参数内容对服务端保持不透明。</summary>
public sealed class NetworkRpcManager
{
    private const int MaxArgumentsBytes = 64 * 1024;

    public static NetworkRpcManager Instance { get; } = new();

    public async Task RelayAsync(PlayerSession sender, NetworkRpcData? input)
    {
        if (input == null ||
            input.ObjectId == 0 ||
            input.BehaviourId == 0 ||
            input.MethodId == 0 ||
            input.Arguments.Length > MaxArgumentsBytes ||
            sender.NetworkRoomId == 0)
        {
            return;
        }

        uint roomId;
        if (input.ObjectId == sender.NetworkObjectId)
            roomId = sender.NetworkRoomId;
        else if (!NetworkRoomObjectManager.Instance.TryGetAuthorizedRoomId(sender, input.ObjectId, out roomId))
            return;

        NetworkRpcData rpc = input.Clone();
        await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRpcInvoke,
            NetworkRpc = rpc
        }.ToByteArray(), rpc.IncludeSender ? null : sender.PlayerId, roomId);
    }
}
