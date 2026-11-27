using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using GameData;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using WebSocketDemo;
using WebSocketDemo.Transport;

var testConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["WeChatMiniGame:ServerVersion"] = "1008" })
    .Build();
ServerVersionProtocol.Configure(testConfig);

using var serverCts = new CancellationTokenSource();
var tcpService = new TcpServerService(0);
var kcpService = new KcpServerService(0);

await tcpService.StartAsync(serverCts.Token);
await kcpService.StartAsync(serverCts.Token);

// 等待两个监听端口就绪
await WaitUntilAsync(() => tcpService.ListeningPort > 0 && kcpService.ListeningPort > 0, 3000, "Listeners did not bind");
int tcpPort = tcpService.ListeningPort;
int kcpPort = kcpService.ListeningPort;

try
{
    await TestPureTcpAsync(tcpPort);
    Console.WriteLine("PASS: Pure TCP (bind info, Protobuf RPC, Ping/Pong, 4001 close code)");

    await TestPureKcpAsync(kcpPort);
    Console.WriteLine("PASS: Pure KCP (idempotent handshake, Protobuf RPC, >4KB fragmentation/reassembly, close)");

    await TestHybridTcpAndKcpAsync(tcpPort, kcpPort);
    Console.WriteLine("PASS: Hybrid TCP+KCP (reject bad token, valid bind, TCP reliable + KCP realtime routing, KCP drop fallback to TCP)");

    await TestTcpCoalescedBatchFramesAsync(tcpPort);
    Console.WriteLine("PASS: TCP coalesced multi-frame burst (60 frames in single write parsed cleanly by buffered reader)");

    await TestHybridConcurrentStressAsync(tcpPort, kcpPort);
    Console.WriteLine("PASS: Hybrid TCP+KCP concurrent stress (16 sessions, mixed reliable + realtime stream + latest bursts)");

    Console.WriteLine("All transport protocol checks passed.");
}
finally
{
    serverCts.Cancel();
    await tcpService.StopAsync(CancellationToken.None);
    await kcpService.StopAsync(CancellationToken.None);
}

static async Task TestPureTcpAsync(int tcpPort)
{
    using var client = new TcpClient();
    client.NoDelay = true;
    await client.ConnectAsync(IPAddress.Loopback, tcpPort);
    using NetworkStream stream = client.GetStream();

    // 1. 建连后应立即收到服务端下发的 FrameBindInfo (conv + 16B token)
    var (bindFrameType, bindPayload) = await ReadTcpFrameAsync(stream, 3000);
    Assert(bindFrameType == TransportProtocol.FrameBindInfo, $"Expected FrameBindInfo, got {bindFrameType}");
    Assert(bindPayload.Length == 4 + TransportProtocol.BindTokenBytes, $"Unexpected bind info length: {bindPayload.Length}");
    uint conv = BinaryPrimitives.ReadUInt32LittleEndian(bindPayload.AsSpan(0, 4));
    Assert(conv > 0, "Expected non-zero KCP conv");

    // 2. 发送 Protobuf ServerVersionQuery 并校验响应
    string reqId = Guid.NewGuid().ToString("N");
    var req = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
    req.DataDic["__server_version_query"] = "1";
    req.DataDic["__web_request_id"] = reqId;
    await WriteTcpFrameAsync(stream, TransportProtocol.FrameBinary, req.ToByteArray());

    var (respType, respPayload) = await ReadTcpFrameAsync(stream, 3000);
    Assert(respType == TransportProtocol.FrameBinary, $"Expected FrameBinary, got {respType}");
    Msg resp = Msg.Parser.ParseFrom(respPayload);
    Assert(resp.DataDic.TryGetValue("__web_response", out string? respId) && respId == reqId, "Mismatched __web_response");
    Assert(resp.DataDic.ContainsKey("__server_version"), "Missing __server_version in TCP response");

    // 3. Ping / Pong
    byte[] pingData = Encoding.UTF8.GetBytes("tcp-ping-payload");
    await WriteTcpFrameAsync(stream, TransportProtocol.FramePing, pingData);
    var (pongType, pongPayload) = await ReadTcpFrameAsync(stream, 3000);
    Assert(pongType == TransportProtocol.FramePong, $"Expected FramePong, got {pongType}");
    Assert(pongPayload.SequenceEqual(pingData), "TCP Pong payload mismatch");

    // 4. 服务端主动发起账号替换关闭（4001），客户端应收到携带 4001 关闭码的 FrameClose
    byte[] bindToken = bindPayload.AsSpan(4, TransportProtocol.BindTokenBytes).ToArray();
    PlayerSession session = FindSessionByBind(conv, bindToken);
    await session.CloseForReplacementAsync();

    var (closeType, closePayload) = await ReadTcpFrameAsync(stream, 3000);
    Assert(closeType == TransportProtocol.FrameClose, $"Expected FrameClose, got {closeType}");
    var (closeStatus, _) = TransportProtocol.ParseClosePayload(closePayload);
    Assert((int)closeStatus == 4001, $"Expected close code 4001, got {(int)closeStatus}");
}

static async Task TestPureKcpAsync(int kcpPort)
{
    using var udp = new UdpClient();
    udp.Connect(IPAddress.Loopback, kcpPort);

    uint nonce = 0x1234ABCD;
    byte[] reqPkt = new byte[5];
    reqPkt[0] = TransportProtocol.UdpConnectReq;
    BinaryPrimitives.WriteUInt32LittleEndian(reqPkt.AsSpan(1, 4), nonce);

    // 1. 首次握手
    await udp.SendAsync(reqPkt);
    byte[] ack1 = await ReceiveUdpWithTimeoutAsync(udp, 3000);
    Assert(ack1.Length == 9 && ack1[0] == TransportProtocol.UdpConnectAck, "Expected UdpConnectAck");
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(ack1.AsSpan(1, 4)) == nonce, "Nonce mismatch");
    uint conv = BinaryPrimitives.ReadUInt32LittleEndian(ack1.AsSpan(5, 4));
    Assert(conv > 0, "Expected non-zero conv");

    // 2. 幂等重试：同一 (endpoint, nonce) 应返回同一个 conv
    await udp.SendAsync(reqPkt);
    byte[] ack2 = await ReceiveUdpWithTimeoutAsync(udp, 3000);
    Assert(ack2.Length == 9 && ack2[0] == TransportProtocol.UdpConnectAck, "Expected retry UdpConnectAck");
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(ack2.AsSpan(5, 4)) == conv, "Retry conv should be identical");

    var kcp = CreateTestKcp(conv, udp);

    // 3. 发送 Protobuf ServerVersionQuery 并校验响应
    string reqId = Guid.NewGuid().ToString("N");
    var req = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
    req.DataDic["__server_version_query"] = "1";
    req.DataDic["__web_request_id"] = reqId;
    SendKcpFrame(kcp, TransportProtocol.FrameBinary, req.ToByteArray());

    var (respType, respPayload) = await PumpAndReceiveKcpFrameAsync(udp, kcp, 3000);
    Assert(respType == TransportProtocol.FrameBinary, $"Expected KCP FrameBinary, got {respType}");
    Msg resp = Msg.Parser.ParseFrom(respPayload);
    Assert(resp.DataDic.TryGetValue("__web_response", out string? respId) && respId == reqId, "KCP __web_response mismatch");
    Assert(resp.DataDic.ContainsKey("__server_version"), "Missing __server_version in KCP response");

    // 4. 多分片（>4KB，跨 4 个 KCP MSS 分片）Ping -> Pong 完整性验证
    byte[] bigPayload = new byte[4500];
    for (int i = 0; i < bigPayload.Length; i++) bigPayload[i] = (byte)(i * 31 + 7);
    SendKcpFrame(kcp, TransportProtocol.FramePing, bigPayload);

    var (pongType, pongPayload) = await PumpAndReceiveKcpFrameAsync(udp, kcp, 4000);
    Assert(pongType == TransportProtocol.FramePong, $"Expected KCP FramePong, got {pongType}");
    Assert(pongPayload.SequenceEqual(bigPayload), "KCP multi-segment Pong payload mismatch");

    // 5. 关闭 KCP 连接
    byte[] udpClose = new byte[7];
    udpClose[0] = TransportProtocol.UdpClose;
    BinaryPrimitives.WriteUInt32LittleEndian(udpClose.AsSpan(1, 4), conv);
    BinaryPrimitives.WriteUInt16LittleEndian(udpClose.AsSpan(5, 2), (ushort)WebSocketCloseStatus.NormalClosure);
    await udp.SendAsync(udpClose);
}

static async Task TestHybridTcpAndKcpAsync(int tcpPort, int kcpPort)
{
    using var tcp = new TcpClient();
    tcp.NoDelay = true;
    await tcp.ConnectAsync(IPAddress.Loopback, tcpPort);
    using NetworkStream stream = tcp.GetStream();

    var (bindFrameType, bindPayload) = await ReadTcpFrameAsync(stream, 3000);
    Assert(bindFrameType == TransportProtocol.FrameBindInfo, "Expected FrameBindInfo on hybrid TCP");
    uint conv = BinaryPrimitives.ReadUInt32LittleEndian(bindPayload.AsSpan(0, 4));
    byte[] validToken = bindPayload.AsSpan(4, TransportProtocol.BindTokenBytes).ToArray();
    PlayerSession session = FindSessionByBind(conv, validToken);

    using var udp = new UdpClient();
    udp.Connect(IPAddress.Loopback, kcpPort);

    // 1. 错误令牌应被拒绝
    byte[] badBind = new byte[5 + TransportProtocol.BindTokenBytes];
    badBind[0] = TransportProtocol.UdpBindReq;
    BinaryPrimitives.WriteUInt32LittleEndian(badBind.AsSpan(1, 4), conv);
    badBind[5] = (byte)(validToken[0] ^ 0xFF);
    await udp.SendAsync(badBind);
    byte[] reject = await ReceiveUdpWithTimeoutAsync(udp, 3000);
    Assert(reject.Length == 5 && reject[0] == TransportProtocol.UdpBindReject, "Expected UdpBindReject for invalid token");
    Assert(!session.HasRealtimeTransport, "Session should not have realtime transport after bad token");

    // 2. 正确令牌应绑定成功
    byte[] goodBind = new byte[5 + TransportProtocol.BindTokenBytes];
    goodBind[0] = TransportProtocol.UdpBindReq;
    BinaryPrimitives.WriteUInt32LittleEndian(goodBind.AsSpan(1, 4), conv);
    Buffer.BlockCopy(validToken, 0, goodBind, 5, TransportProtocol.BindTokenBytes);
    await udp.SendAsync(goodBind);
    byte[] bindAck = await ReceiveUdpWithTimeoutAsync(udp, 3000);
    Assert(bindAck.Length == 5 && bindAck[0] == TransportProtocol.UdpBindAck, "Expected UdpBindAck");
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(bindAck.AsSpan(1, 4)) == conv, "BindAck conv mismatch");
    Assert(session.HasRealtimeTransport, "Session should have realtime transport attached");
    Assert(session.TransportName == "TCP+KCP", $"Expected TransportName TCP+KCP, got {session.TransportName}");

    var kcp = CreateTestKcp(conv, udp);

    // 3. 可靠消息走 TCP
    byte[] reliableMsg = Encoding.UTF8.GetBytes("reliable-over-tcp");
    await session.SendBinaryAsync(reliableMsg);
    var (tcpType, tcpData) = await ReadTcpFrameAsync(stream, 3000);
    Assert(tcpType == TransportProtocol.FrameBinary && tcpData.SequenceEqual(reliableMsg), "Reliable message did not arrive on TCP");

    // 4. 实时消息（QueueLatestBinary）走 KCP
    byte[] realtimeMsg = Encoding.UTF8.GetBytes("realtime-over-kcp");
    session.QueueLatestBinary(42UL, realtimeMsg);
    var (kcpType, kcpData) = await PumpAndReceiveKcpFrameAsync(udp, kcp, 3000);
    Assert(kcpType == TransportProtocol.FrameBinary && kcpData.SequenceEqual(realtimeMsg), "Realtime message did not arrive on KCP");

    // 5. 客户端通过 KCP 上行发送业务消息，服务端应能正常接收并处理
    string kcpReqId = Guid.NewGuid().ToString("N");
    var kcpReq = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
    kcpReq.DataDic["__server_version_query"] = "1";
    kcpReq.DataDic["__web_request_id"] = kcpReqId;
    SendKcpFrame(kcp, TransportProtocol.FrameBinary, kcpReq.ToByteArray());
    // 启动一次 KCP 驱动，确保上行段到达服务端；服务端回复通过可靠 QueueBinary 回到 TCP
    await PumpKcpForDurationAsync(udp, kcp, 80);
    var (replyType, replyData) = await ReadTcpFrameAsync(stream, 3000);
    Assert(replyType == TransportProtocol.FrameBinary, "Expected TCP reply to KCP uplink query");
    Msg reply = Msg.Parser.ParseFrom(replyData);
    Assert(reply.DataDic.TryGetValue("__web_response", out string? gotId) && gotId == kcpReqId, "Hybrid uplink response mismatch");

    // 5b. 客户端通过 0x11 非可靠有序 UDP 上行发送数据包（错误 authTag 应被静默拦截，正确 authTag 立即送达）
    uint validAuthTag = BinaryPrimitives.ReadUInt32LittleEndian(validToken.AsSpan(0, 4));
    string badReqId = Guid.NewGuid().ToString("N");
    var badUplink = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
    badUplink.DataDic["__server_version_query"] = "1";
    badUplink.DataDic["__web_request_id"] = badReqId;
    await SendUnreliableUdpDatagramAsync(udp, conv, validAuthTag ^ 0xDEADBEEFu, 1, badUplink.ToByteArray());

    string fastReqId = Guid.NewGuid().ToString("N");
    var fastUplink = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
    fastUplink.DataDic["__server_version_query"] = "1";
    fastUplink.DataDic["__web_request_id"] = fastReqId;
    await SendUnreliableUdpDatagramAsync(udp, conv, validAuthTag, 2, fastUplink.ToByteArray());

    var (uReplyType, uReplyData) = await ReadTcpFrameAsync(stream, 3000);
    Assert(uReplyType == TransportProtocol.FrameBinary, "Expected TCP reply to 0x11 unreliable uplink");
    Msg uReply = Msg.Parser.ParseFrom(uReplyData);
    Assert(uReply.DataDic.TryGetValue("__web_response", out string? uGotId) && uGotId == fastReqId,
        $"Expected response for valid 0x11 packet ({fastReqId}), got {uGotId} (forged packet must be dropped)");

    // 6. KCP 断开后：TCP 会话不断开，后续实时消息自动回退到 TCP
    byte[] udpClose = new byte[7];
    udpClose[0] = TransportProtocol.UdpClose;
    BinaryPrimitives.WriteUInt32LittleEndian(udpClose.AsSpan(1, 4), conv);
    BinaryPrimitives.WriteUInt16LittleEndian(udpClose.AsSpan(5, 2), (ushort)WebSocketCloseStatus.NormalClosure);
    await udp.SendAsync(udpClose);

    await WaitUntilAsync(() => !session.HasRealtimeTransport, 3000, "Realtime transport did not detach after UdpClose");
    Assert(session.IsConnected, "TCP primary session should stay connected after KCP closes");

    byte[] fallbackMsg = Encoding.UTF8.GetBytes("realtime-fallback-to-tcp");
    session.QueueLatestBinary(99UL, fallbackMsg);
    var (fbType, fbData) = await ReadTcpFrameAsync(stream, 3000);
    Assert(fbType == TransportProtocol.FrameBinary && fbData.SequenceEqual(fallbackMsg), "Realtime fallback message did not arrive on TCP");

    // 7. 正常关闭 TCP
    await WriteTcpFrameAsync(stream, TransportProtocol.FrameClose,
        TransportProtocol.BuildClosePayload(WebSocketCloseStatus.NormalClosure, "bye"));
}

static async Task TestTcpCoalescedBatchFramesAsync(int tcpPort)
{
    using var client = new TcpClient();
    client.NoDelay = true;
    await client.ConnectAsync(IPAddress.Loopback, tcpPort);
    using NetworkStream stream = client.GetStream();

    var (bindType, _) = await ReadTcpFrameAsync(stream, 3000);
    Assert(bindType == TransportProtocol.FrameBindInfo, "Expected initial FrameBindInfo");

    const int frameCount = 60;
    using var batchStream = new MemoryStream();
    var expectedPayloads = new List<byte[]>(frameCount);
    byte[] hdr = new byte[5];
    for (int i = 0; i < frameCount; i++)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"coalesced-ping-{i:D3}-" + new string((char)('a' + (i % 26)), (i % 37) + 1));
        expectedPayloads.Add(payload);
        BinaryPrimitives.WriteInt32LittleEndian(hdr.AsSpan(0, 4), payload.Length);
        hdr[4] = TransportProtocol.FramePing;
        batchStream.Write(hdr, 0, 5);
        batchStream.Write(payload, 0, payload.Length);
    }

    // 单次 WriteAsync 写入全部 60 帧，验证服务端读缓冲批解析无粘包/拆包丢帧
    byte[] combined = batchStream.ToArray();
    await stream.WriteAsync(combined);

    for (int i = 0; i < frameCount; i++)
    {
        var (pongType, pongPayload) = await ReadTcpFrameAsync(stream, 4000);
        Assert(pongType == TransportProtocol.FramePong, $"Frame {i}: expected FramePong, got {pongType}");
        Assert(pongPayload.SequenceEqual(expectedPayloads[i]), $"Frame {i}: coalesced Pong payload mismatch");
    }

    await WriteTcpFrameAsync(stream, TransportProtocol.FrameClose,
        TransportProtocol.BuildClosePayload(WebSocketCloseStatus.NormalClosure, "batch-done"));
}

static async Task TestHybridConcurrentStressAsync(int tcpPort, int kcpPort)
{
    const int clientCount = 16;
    var tasks = new Task[clientCount];
    for (int idx = 0; idx < clientCount; idx++)
    {
        int workerId = idx;
        tasks[idx] = Task.Run(async () =>
        {
            using var tcp = new TcpClient();
            tcp.NoDelay = true;
            await tcp.ConnectAsync(IPAddress.Loopback, tcpPort);
            using NetworkStream stream = tcp.GetStream();

            var (bindType, bindPayload) = await ReadTcpFrameAsync(stream, 5000);
            Assert(bindType == TransportProtocol.FrameBindInfo, $"Worker {workerId}: missing FrameBindInfo");
            uint conv = BinaryPrimitives.ReadUInt32LittleEndian(bindPayload.AsSpan(0, 4));
            byte[] token = bindPayload.AsSpan(4, TransportProtocol.BindTokenBytes).ToArray();
            PlayerSession session = FindSessionByBind(conv, token);

            using var udp = new UdpClient();
            udp.Connect(IPAddress.Loopback, kcpPort);

            byte[] bindReq = new byte[5 + TransportProtocol.BindTokenBytes];
            bindReq[0] = TransportProtocol.UdpBindReq;
            BinaryPrimitives.WriteUInt32LittleEndian(bindReq.AsSpan(1, 4), conv);
            Buffer.BlockCopy(token, 0, bindReq, 5, TransportProtocol.BindTokenBytes);
            await udp.SendAsync(bindReq);

            byte[] bindAck = await ReceiveUdpWithTimeoutAsync(udp, 5000);
            Assert(bindAck.Length == 5 && bindAck[0] == TransportProtocol.UdpBindAck, $"Worker {workerId}: UdpBindAck failed");

            using var kcp = CreateTestKcp(conv, udp);

            // 混合下发：5 条非覆写实时语音流（QueueRealtimeBinary -> KCP）+ 1 条可靠消息（TCP）
            for (int m = 0; m < 5; m++)
            {
                byte[] voiceFrame = Encoding.UTF8.GetBytes($"voice-{workerId}-{m}");
                session.QueueRealtimeBinary(voiceFrame, maxAgeMs: 2000);
            }

            byte[] tcpMarker = Encoding.UTF8.GetBytes($"tcp-reliable-{workerId}");
            await session.SendBinaryAsync(tcpMarker);

            var (gotTcpType, gotTcpPayload) = await ReadTcpFrameAsync(stream, 5000);
            Assert(gotTcpType == TransportProtocol.FrameBinary && gotTcpPayload.SequenceEqual(tcpMarker),
                $"Worker {workerId}: TCP reliable payload mismatch");

            for (int m = 0; m < 5; m++)
            {
                var (gotKcpType, gotKcpPayload) = await PumpAndReceiveKcpFrameAsync(udp, kcp, 5000);
                string text = Encoding.UTF8.GetString(gotKcpPayload);
                Assert(gotKcpType == TransportProtocol.FrameBinary && text == $"voice-{workerId}-{m}",
                    $"Worker {workerId}: KCP realtime stream mismatch at {m}: {text}");
            }

            await WriteTcpFrameAsync(stream, TransportProtocol.FrameClose,
                TransportProtocol.BuildClosePayload(WebSocketCloseStatus.NormalClosure, "stress-done"));
        });
    }

    await Task.WhenAll(tasks);
}

static PlayerSession FindSessionByBind(uint conv, byte[] bindToken)
{
    bool ok = KcpBindRegistry.TryValidateAndGetSession(conv, bindToken, out PlayerSession? found);
    Assert(ok && found != null, $"Could not find session for conv {conv}");
    return found!;
}

static KcpCore CreateTestKcp(uint conv, UdpClient udp)
{
    var kcp = new KcpCore(conv, (buf, len) =>
    {
        byte[] datagram = new byte[1 + len];
        datagram[0] = TransportProtocol.UdpKcpData;
        Buffer.BlockCopy(buf, 0, datagram, 1, len);
        udp.Send(datagram, datagram.Length);
    });
    kcp.SetNoDelay(1, 10, 2, 1);
    kcp.SetWndSize(128, 128);
    kcp.SetMtu(1200);
    kcp.Update(unchecked((uint)Environment.TickCount64));
    return kcp;
}

static Task SendUnreliableUdpDatagramAsync(UdpClient udp, uint conv, uint authTag, uint seq, byte[] payload)
{
    byte[] pkt = new byte[TransportProtocol.UnreliableHeaderBytes + payload.Length];
    pkt[0] = TransportProtocol.UdpUnreliableData;
    BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(1, 4), conv);
    BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(5, 4), authTag);
    BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(9, 4), seq);
    pkt[13] = TransportProtocol.FrameBinary;
    if (payload.Length > 0)
        Buffer.BlockCopy(payload, 0, pkt, TransportProtocol.UnreliableHeaderBytes, payload.Length);
    return udp.SendAsync(pkt, pkt.Length);
}

static void SendKcpFrame(KcpCore kcp, byte frameType, byte[] payload)
{
    byte[] frame = new byte[1 + payload.Length];
    frame[0] = frameType;
    if (payload.Length > 0) Buffer.BlockCopy(payload, 0, frame, 1, payload.Length);
    int rc = kcp.Send(frame);
    Assert(rc == 0, $"KcpCore.Send failed with {rc}");
    kcp.Flush();
}

static async Task PumpKcpForDurationAsync(UdpClient udp, KcpCore kcp, int durationMs)
{
    long end = Environment.TickCount64 + durationMs;
    while (Environment.TickCount64 < end)
    {
        kcp.Update(unchecked((uint)Environment.TickCount64));
        while (udp.Available > 0)
        {
            var r = await udp.ReceiveAsync();
            if (r.Buffer.Length >= 1 + KcpCore.Overhead && r.Buffer[0] == TransportProtocol.UdpKcpData)
            {
                kcp.Input(r.Buffer.AsSpan(1));
                kcp.Flush();
            }
        }
        await Task.Delay(10);
    }
}

static async Task<(byte FrameType, byte[] Payload)> PumpAndReceiveKcpFrameAsync(UdpClient udp, KcpCore kcp, int timeoutMs)
{
    long deadline = Environment.TickCount64 + timeoutMs;
    while (Environment.TickCount64 < deadline)
    {
        kcp.Update(unchecked((uint)Environment.TickCount64));
        while (udp.Available > 0)
        {
            var r = await udp.ReceiveAsync();
            if (r.Buffer.Length >= TransportProtocol.UnreliableHeaderBytes &&
                r.Buffer[0] == TransportProtocol.UdpUnreliableData)
            {
                byte ft = r.Buffer[13];
                int pLen = r.Buffer.Length - TransportProtocol.UnreliableHeaderBytes;
                byte[] pData = pLen > 0 ? r.Buffer.AsSpan(TransportProtocol.UnreliableHeaderBytes, pLen).ToArray() : Array.Empty<byte>();
                return (ft, pData);
            }
            if (r.Buffer.Length >= 1 + KcpCore.Overhead && r.Buffer[0] == TransportProtocol.UdpKcpData)
            {
                kcp.Input(r.Buffer.AsSpan(1));
                kcp.Flush();
            }
        }

        int size = kcp.PeekSize();
        if (size > 0)
        {
            byte[] buf = new byte[size];
            int read = kcp.Recv(buf);
            Assert(read > 0, "KcpCore.Recv returned <= 0");
            byte frameType = buf[0];
            byte[] payload = read > 1 ? buf.AsSpan(1, read - 1).ToArray() : Array.Empty<byte>();
            return (frameType, payload);
        }

        await Task.Delay(10);
    }

    throw new TimeoutException($"Timed out waiting for KCP frame after {timeoutMs}ms");
}

static async Task<byte[]> ReceiveUdpWithTimeoutAsync(UdpClient udp, int timeoutMs)
{
    using var cts = new CancellationTokenSource(timeoutMs);
    var result = await udp.ReceiveAsync(cts.Token);
    return result.Buffer;
}

static async Task WriteTcpFrameAsync(NetworkStream stream, byte frameType, byte[] payload)
{
    byte[] packet = new byte[5 + payload.Length];
    BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0, 4), payload.Length);
    packet[4] = frameType;
    if (payload.Length > 0) Buffer.BlockCopy(payload, 0, packet, 5, payload.Length);
    await stream.WriteAsync(packet);
}

static async Task<(byte FrameType, byte[] Payload)> ReadTcpFrameAsync(NetworkStream stream, int timeoutMs)
{
    using var cts = new CancellationTokenSource(timeoutMs);
    byte[] header = new byte[5];
    await ReadExactAsync(stream, header, cts.Token);
    int len = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
    byte frameType = header[4];
    byte[] payload = len > 0 ? new byte[len] : Array.Empty<byte>();
    if (len > 0) await ReadExactAsync(stream, payload, cts.Token);
    return (frameType, payload);
}

static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
{
    int offset = 0;
    while (offset < buffer.Length)
    {
        int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token);
        if (read <= 0) throw new EndOfStreamException("Unexpected TCP EOF");
        offset += read;
    }
}

static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs, string message)
{
    long deadline = Environment.TickCount64 + timeoutMs;
    while (Environment.TickCount64 < deadline)
    {
        if (predicate()) return;
        await Task.Delay(15);
    }
    throw new TimeoutException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("ASSERT FAILED: " + message);
}
