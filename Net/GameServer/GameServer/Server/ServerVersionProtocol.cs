using GameData;

namespace WebSocketDemo;

/// <summary>登录前可用的只读版本查询，复用 Msg.DataDic 和现有请求关联 ID。</summary>
public static class ServerVersionProtocol
{
    public const string QueryKey = "__server_version_query";
    public const string VersionKey = "__server_version";
    public const string LatencyProbeKey = "__latency_probe";
    public const string QueuePacketsKey = "__server_queue_packets";
    public const string QueueBytesKey = "__server_queue_bytes";
    public const string QueueWaitMsKey = "__server_queue_wait_ms";
    public const string WriteMsKey = "__server_write_ms";
    public const string ExpiredRealtimeKey = "__server_expired_realtime";
    private static IConfiguration? _configuration;

    public static void Configure(IConfiguration configuration) => _configuration = configuration;

    public static bool IsQuery(Msg request) => request.MsgType == ProtobufMsgType.Game &&
        request.GameMsgType == GameMsgType.GetServerList &&
        request.DataDic.TryGetValue(QueryKey, out var value) && value == "1";

    public static Msg CreateResponse()
    {
        var version = (_configuration?["WeChatMiniGame:ServerVersion"] ??
                       _configuration?["WeChatMiniGame:ClientVersion"])?.Trim();
        var response = new Msg { MsgType = ProtobufMsgType.Server, ServerMsgType = ServerMsgType.Tips };
        response.DataDic[QueryKey] = "1";
        if (string.IsNullOrWhiteSpace(version))
        {
            response.DataDic["__web_response_error"] = "服务器未配置 ServerVersion，请联系管理员。";
            response.DataDic["__web_response_error_code"] = "SERVER_VERSION_NOT_CONFIGURED";
        }
        else
            response.DataDic[VersionKey] = version;
        return response;
    }

    public static void AddLatencyStats(Msg request, Msg response, PlayerSession session)
    {
        if (!request.DataDic.TryGetValue(LatencyProbeKey, out string? probe) || probe != "1")
            return;
        var stats = session.SendQueueStats;
        response.DataDic[QueuePacketsKey] = stats.Packets.ToString();
        response.DataDic[QueueBytesKey] = stats.Bytes.ToString();
        response.DataDic[QueueWaitMsKey] = stats.WaitMs.ToString();
        response.DataDic[WriteMsKey] = stats.WriteMs.ToString();
        response.DataDic[ExpiredRealtimeKey] = stats.Expired.ToString();
    }
}
