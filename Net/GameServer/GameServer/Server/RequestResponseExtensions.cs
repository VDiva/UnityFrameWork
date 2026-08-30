using GameData;
using Google.Protobuf;

namespace WebSocketDemo;

/// <summary>
/// 为现有 Msg 协议提供可选的一问一答模式。元数据存放在 DataDic，旧客户端和旧广播不受影响。
/// </summary>
public static class RequestResponseExtensions
{
    private const string RequestIdKey = "__web_request_id";
    private const string ResponseKey = "__web_response";
    private const string ResponseErrorKey = "__web_response_error";
    private const string ResponseErrorCodeKey = "__web_response_error_code";

    public static bool IsRequest(this Msg request) =>
        request != null && request.DataDic.TryGetValue(RequestIdKey, out string id) &&
        !string.IsNullOrWhiteSpace(id);

    public static Dictionary<string, string> GetBusinessData(this Msg request)
    {
        return request.DataDic
            .Where(pair => pair.Key != RequestIdKey && pair.Key != ResponseKey &&
                           pair.Key != ResponseErrorKey && pair.Key != ResponseErrorCodeKey)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static Task ReplyAsync(this PlayerSession player, Msg request, Msg response)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        response.MsgType = ProtobufMsgType.Server;
        CopyRequestId(request, response);
        return player.SendBinaryAsync(response.ToByteArray());
    }

    public static Task ReplyErrorAsync(this PlayerSession player, Msg request, string error)
    {
        return player.ReplyErrorAsync(request, "SERVER_ERROR", error);
    }

    public static Task ReplyErrorAsync(
        this PlayerSession player, Msg request, string errorCode, string error)
    {
        var response = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.Tips,
            TipsSrt = error ?? "服务器处理请求失败。"
        };
        CopyRequestId(request, response);
        response.DataDic[ResponseErrorKey] = response.TipsSrt;
        response.DataDic[ResponseErrorCodeKey] =
            string.IsNullOrWhiteSpace(errorCode) ? "SERVER_ERROR" : errorCode;
        return player.SendBinaryAsync(response.ToByteArray());
    }

    private static void CopyRequestId(Msg request, Msg response)
    {
        if (request != null && request.DataDic.TryGetValue(RequestIdKey, out string requestId) &&
            !string.IsNullOrWhiteSpace(requestId))
            response.DataDic[ResponseKey] = requestId;
    }
}
