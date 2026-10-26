using System.Net.WebSockets;
using System.Threading.Channels;
using GameData;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using WebSocketDemo;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
Check(!FrameWork.ServerVersionPolicy.RequiresUpdate("100", "100"), "equal version allowed");
Check(!FrameWork.ServerVersionPolicy.RequiresUpdate("101", "100"), "newer client allowed");
Check(FrameWork.ServerVersionPolicy.RequiresUpdate("99", "100"), "older client requires update");
Check(!FrameWork.ServerVersionPolicy.RequiresUpdate("100", "99"), "numeric order, not string order");
Check(!FrameWork.ServerVersionPolicy.RequiresUpdate(" 00100 ", "100"), "whitespace and leading zeros allowed");
Check(!FrameWork.ServerVersionPolicy.RequiresUpdate("9223372036854775807", "100"), "large integer version allowed");
foreach (var invalid in new[] { "", "-1", "abc", "1.0", "9223372036854775808" })
{
    bool rejected = false;
    try { FrameWork.ServerVersionPolicy.RequiresUpdate(invalid, "100"); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "invalid client version rejected: " + invalid);
    rejected = false;
    try { FrameWork.ServerVersionPolicy.RequiresUpdate("100", invalid); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "invalid server version rejected: " + invalid);
}
ServerVersionProtocol.Configure(configuration);
var request = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
Check(!ServerVersionProtocol.IsQuery(request), "ordinary server list is unchanged");
request.DataDic[ServerVersionProtocol.QueryKey] = "1";
Check(ServerVersionProtocol.IsQuery(request), "version query recognized before login");
var other = request.Clone();
other.GameMsgType = GameMsgType.Login;
Check(!ServerVersionProtocol.IsQuery(other), "login is not interpreted as version query");

using var socket = new TestSocket();
using var session = new PlayerSession("version-check-test", socket);
async Task<Msg> Query(string id)
{
    request.DataDic["__web_request_id"] = id;
    await socket.Incoming.Writer.WriteAsync(request.ToByteArray());
    var payload = await socket.Outgoing.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    var reply = Msg.Parser.ParseFrom(payload);
    Check(reply.DataDic["__web_response"] == id, "response belongs to request " + id);
    Check(reply.MsgType == ProtobufMsgType.Server && reply.DataDic[ServerVersionProtocol.QueryKey] == "1",
        "response is marked as version control message");
    return reply;
}

configuration["WeChatMiniGame:ServerVersion"] = " 100 ";
var first = await Query("first");
Check(first.DataDic[ServerVersionProtocol.VersionKey] == "100", "trimmed version over binary WebSocket");
Check(session.UserId == null, "version query does not authenticate a player");
configuration["WeChatMiniGame:ServerVersion"] = "101";
var changed = await Query("changed");
Check(changed.DataDic[ServerVersionProtocol.VersionKey] == "101", "same connection sees latest configuration");
configuration["WeChatMiniGame:ServerVersion"] = "";
configuration["WeChatMiniGame:ClientVersion"] = "legacy";
var empty = await Query("empty");
Check(empty.DataDic.ContainsKey("__web_response_error") && !empty.DataDic.ContainsKey(ServerVersionProtocol.VersionKey),
    "empty new configuration fails closed instead of using legacy");
configuration["WeChatMiniGame:ServerVersion"] = null;
var legacy = await Query("legacy");
Check(legacy.DataDic[ServerVersionProtocol.VersionKey] == "legacy", "missing new configuration supports migration");
configuration["WeChatMiniGame:ClientVersion"] = null;
var missing = await Query("missing");
Check(missing.DataDic["__web_response_error_code"] == "SERVER_VERSION_NOT_CONFIGURED", "missing configuration gives explicit error");
Console.WriteLine($"Passed {passed} checks.");

sealed class TestSocket : WebSocket
{
    public readonly Channel<byte[]> Incoming = Channel.CreateUnbounded<byte[]>();
    public readonly Channel<byte[]> Outgoing = Channel.CreateUnbounded<byte[]>();
    private WebSocketState _state = WebSocketState.Open;
    public override WebSocketState State => _state;
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;
    public override void Abort() => _state = WebSocketState.Aborted;
    public override void Dispose() => _state = WebSocketState.Closed;
    public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token)
    { _state = WebSocketState.Closed; return Task.CompletedTask; }
    public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token)
        => CloseAsync(status, description, token);
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
    {
        var bytes = await Incoming.Reader.ReadAsync(token);
        bytes.CopyTo(buffer.AsSpan());
        return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Binary, true);
    }
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token)
    {
        if (type != WebSocketMessageType.Binary || !end) throw new Exception("Expected binary protobuf message");
        return Outgoing.Writer.WriteAsync(buffer.ToArray(), token).AsTask();
    }
}
