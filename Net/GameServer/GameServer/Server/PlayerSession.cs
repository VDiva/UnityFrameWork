using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo
{
    public class PlayerSession : IDisposable
    {
        private const int ReceiveBufferSize = 4096;
        private const int MaxMessageSize = 1024 * 1024;
        private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

        private readonly WebSocket _webSocket;
        private readonly CancellationTokenSource _cts;
        private readonly CancellationTokenRegistration _requestAbortedRegistration;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _userDataLock = new SemaphoreSlim(1, 1);
        private readonly SessionSendQueue _sendQueue;
        private readonly SessionCommandQueue _commands = new(exception =>
            Console.WriteLine($"[CommandQueue] {exception.Message}"));
        private readonly Task _receiveTask;
        private readonly TaskCompletionSource<bool> _closeTcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _closeStarted;
        private int _offlineSaved;
        private int _disposed;
        private readonly object _closeGate = new();
        private Task _closeTask = Task.CompletedTask;
        private long _lastSlowHandlerLog;

        public string PlayerId { get; }
        public bool IsSuperseded { get; private set; }

        public Task CloseForReplacementAsync()
        {
            IsSuperseded = true;
            return CloseAsync((WebSocketCloseStatus)4001, "Session replaced");
        }
        public string? UserId { get; private set; }
        /// <summary>登录后固定的逻辑服务器 id，后续消息不能切换。</summary>
        public string? ServerId { get; private set; }
        /// <summary>登录后保存在会话中的玩家数据快照，供房间状态同步使用。</summary>
        public UserData? UserDataSnapshot { get; private set; }
        public uint NetworkObjectId { get; internal set; }
        public NetworkTransformData? LastNetworkTransform { get; internal set; }
        public NetworkAnimationData? LastNetworkAnimation { get; internal set; }
        private uint _networkRoomId;
        public uint NetworkRoomId
        {
            get => _networkRoomId;
            internal set
            {
                uint oldRoomId = _networkRoomId;
                if (oldRoomId == value) return;
                _networkRoomId = value;
                PlayerSessionManager.Instance.OnSessionRoomChanged(this, oldRoomId, value);
            }
        }
        public uint PendingNetworkRoomId { get; internal set; }
        public ConcurrentDictionary<uint, byte> VisibleNetworkObjects { get; } = new();
        public ConcurrentDictionary<uint, byte> NetworkRoomInvites { get; } = new();
        public bool IsConnected => _webSocket.State == WebSocketState.Open;
        public (int Packets, int Bytes, long WaitMs, long WriteMs, long Expired)
            SendQueueStats => (_sendQueue.PendingCount, _sendQueue.PendingBytes,
                _sendQueue.LastQueueWaitMs, _sendQueue.LastWriteMs,
                _sendQueue.ExpiredRealtimePackets);
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastActiveAt { get; private set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastChannelChatAt { get; internal set; } = DateTimeOffset.MinValue;

        public PlayerSession(string playerId, WebSocket webSocket, CancellationToken requestAborted = default)
        {
            PlayerId = playerId;
            _webSocket = webSocket;
            _cts = new CancellationTokenSource();
            _requestAbortedRegistration = requestAborted.Register(() => _cts.Cancel());
            _sendQueue = new SessionSendQueue(SendQueuedAsync, OnSendQueueFailed, label: playerId);
            _receiveTask = StartReceivingAsync();
        }

        public async Task WaitForCloseAsync()
        {
            await Task.WhenAny(_receiveTask, _closeTcs.Task);
            await SafeCloseAsync();
        }

        public Task CloseAsync(
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Closing")
        {
            return SafeCloseAsync(closeStatus, statusDescription);
        }

        /// <summary>
        /// 登录成功后绑定业务玩家 id，连接关闭时会用它把 Redis 数据落到 MongoDB。
        /// </summary>
        public Task BindUserAsync(string userId, string serverId)
        {
            if (!string.IsNullOrWhiteSpace(UserId))
                throw new InvalidOperationException("This connection is already bound to a user.");
            UserId = userId;
            ServerId = ServerScope.Normalize(serverId);
            MarkActive();
            return PlayerSessionManager.Instance.BindUserSessionAsync(userId, ServerId, this);
        }

        /// <summary>
        /// 使用最新玩家数据替换当前会话快照；保存克隆以避免外部继续修改同一 protobuf 实例。
        /// </summary>
        /// <param name="userData">已经由服务端读取或保存成功的最新玩家数据。</param>
        public void UpdateUserDataSnapshot(UserData userData)
        {
            UserDataSnapshot = userData.Clone();
        }

        /// <summary>串行修改并保存当前在线玩家快照，避免并发请求以旧数据覆盖新数据。</summary>
        public async Task<UserData> MutateAndSaveUserDataSnapshotAsync(
            Action<UserData> mutation,
            bool updateRank = false)
        {
            if (mutation == null)
                throw new ArgumentNullException(nameof(mutation));
            if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(ServerId))
                throw new InvalidOperationException("玩家尚未登录。");

            await _userDataLock.WaitAsync();
            try
            {
                if (UserDataSnapshot == null)
                    throw new InvalidOperationException("玩家数据尚未加载。");

                var updated = UserDataSnapshot.Clone();
                mutation(updated);
                if (!await GameDataMrg.SaveUserData(UserId, ServerId, updated, updateRank))
                    throw new InvalidOperationException("保存玩家数据失败。");
                UserDataSnapshot = updated;
                return updated.Clone();
            }
            finally
            {
                _userDataLock.Release();
            }
        }

        /// <summary>
        /// 清除当前角色绑定但保留 WebSocket，客户端可继续登录其他 serverId。
        /// 房间和在线索引由 PlayerSessionManager 在调用前清理。
        /// </summary>
        public void ClearUserBinding()
        {
            UserId = null;
            ServerId = null;
            UserDataSnapshot = null;
            MarkActive();
        }

        private async Task StartReceivingAsync()
        {
            var buffer = new byte[ReceiveBufferSize];
            using var messageBuffer = new MemoryStream(ReceiveBufferSize);

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    messageBuffer.SetLength(0);
                    bool singleFrame = false;
                    do
                    {
                        try
                        {
                            result = await _webSocket.ReceiveAsync(
                                new ArraySegment<byte>(buffer),
                                _cts.Token);
                            MarkActive();
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            CompleteClientClose(result.CloseStatus, result.CloseStatusDescription);
                            return;
                        }

                        if (messageBuffer.Length + result.Count > MaxMessageSize)
                        {
                            Console.WriteLine($"[{PlayerId}] Message too large.");
                            _closeTcs.TrySetResult(true);
                            _ = SafeCloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too big");
                            return;
                        }

                        // 大多数位置包一次 Receive 就完整到达，直接从接收缓冲解析，
                        // 避免每包再复制到 MemoryStream。分片消息仍按原顺序拼接。
                        if (result.EndOfMessage && messageBuffer.Length == 0)
                            singleFrame = true;
                        else
                            messageBuffer.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    int messageLength = singleFrame ? result.Count : checked((int)messageBuffer.Length);
                    if (messageLength == 0)
                    {
                        continue;
                    }

                    byte[] fullData = singleFrame ? buffer : messageBuffer.GetBuffer();

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var fullMessage = Encoding.UTF8.GetString(fullData, 0, messageLength);
                        await OnMessageReceived(fullMessage);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await DispatchBinaryMessageAsync(fullData, messageLength);
                    }
                    // 大包只在本次处理期间保留容量，不让每个长连接长期占用 1MB 缓冲区。
                    if (messageBuffer.Capacity > 64 * 1024)
                    {
                        messageBuffer.SetLength(0);
                        messageBuffer.Capacity = ReceiveBufferSize;
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"[{PlayerId}] WebSocket error: {ex.WebSocketErrorCode}, {ex.Message}, socketState={_webSocket.State}, user={UserId ?? "none"}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{PlayerId}] Receive error: {ex.GetType().Name}, {ex.Message}, socketState={_webSocket.State}, user={UserId ?? "none"}.");
            }
            finally
            {
                _closeTcs.TrySetResult(true);
                _ = SafeCloseAsync();
                _cts.Cancel();
                // 已接收的业务写入先结束，再保存离线数据。
                _commands.Complete();
                await _commands.Completion;
                QueueOfflineSave();
            }
        }

        private void CompleteClientClose(WebSocketCloseStatus? closeStatus, string? statusDescription)
        {
            if (Interlocked.Exchange(ref _closeStarted, 1) == 1)
            {
                return;
            }

            _closeTcs.TrySetResult(true);
            try { _cts.Cancel(); } catch { }

            _sendQueue.Stop();
        }

        private Task OnMessageReceived(string message)
        {
            // 文本消息不承载游戏或管理指令，避免未认证连接修改服务端数据。
            return Task.CompletedTask;
        }

        private async Task OnBinaryMessageReceived(Msg msg)
        {
            if (IsSuperseded) return;
            MarkActive();
            long startedAt = Environment.TickCount64;
            try { await MsgMrg.Received(msg, this); }
            finally
            {
                long now = Environment.TickCount64;
                if (now - startedAt > 500 && now - Interlocked.Read(ref _lastSlowHandlerLog) > 5000)
                {
                    Interlocked.Exchange(ref _lastSlowHandlerLog, now);
                    Console.WriteLine($"[HandlerPerf] {PlayerId}: type={msg.GameMsgType}, elapsedMs={now - startedAt}");
                }
            }
        }

        private async Task DispatchBinaryMessageAsync(byte[] data, int length)
        {
            Msg msg;
            try { msg = Msg.Parser.ParseFrom(data, 0, length); }
            catch (InvalidProtocolBufferException) { return; }
            if (msg.MsgType != ProtobufMsgType.Game) return;
            // 版本/延迟探测不访问数据库，直接回复，不排在业务指令之后。
            if (ServerVersionProtocol.IsQuery(msg))
            {
                await OnBinaryMessageReceived(msg);
                return;
            }
            bool realtime = IsRealtimeMessage(msg.GameMsgType);
            if (realtime && !_commands.HasPendingWorldMutation)
            {
                await OnBinaryMessageReceived(msg);
                return;
            }
            // 房间/对象生命周期指令未完成时旧位置已无意义，直接丢弃；
            // 其他实时事件仍按接收顺序排在屏障后面。
            if (msg.GameMsgType == GameMsgType.UploadNetworkTransform) return;
            bool worldMutation = IsWorldMutation(msg.GameMsgType);
            Func<Task> action = async () =>
            {
                if (!IsConnected || IsSuperseded) return;
                await OnBinaryMessageReceived(msg);
            };
            if (worldMutation)
            {
                // 生成/切图不能因短时积压而静默丢失；达到硬上限时对发送者施加背压。
                while (!_commands.TryEnqueue(length, true, action))
                {
                    if (!IsConnected || IsSuperseded) return;
                    try { await Task.Delay(5, _cts.Token); }
                    catch (OperationCanceledException) { return; }
                }
                return;
            }
            if (!_commands.TryEnqueue(length, false, action))
                await this.ReplyErrorAsync(msg, "COMMAND_QUEUE_FULL", "指令队列已满，请稍后重试。");
        }

        private static bool IsRealtimeMessage(GameMsgType type) => type is
            GameMsgType.UploadNetworkTransform or GameMsgType.UploadNetworkAnimation or
            GameMsgType.UploadNetworkSyncVar or GameMsgType.UploadNetworkRpc or
            GameMsgType.UploadNetworkVoice;

        private static bool IsWorldMutation(GameMsgType type) => type is
            GameMsgType.Login or GameMsgType.Logout or GameMsgType.CreateNetworkRoom or
            GameMsgType.MatchNetworkRoom or GameMsgType.NetworkRoomSceneReady or
            GameMsgType.ChangeNetworkRoomMap or GameMsgType.LeaveNetworkRoom or
            GameMsgType.StartNetworkRoomGame or GameMsgType.KickNetworkRoomPlayer or
            GameMsgType.AcceptNetworkRoomInvite or GameMsgType.JoinNetworkRoom or
            GameMsgType.SpawnNetworkRoomObject or GameMsgType.DestroyNetworkRoomObject or
            GameMsgType.SpawnNetworkRoomAi or GameMsgType.SpawnNetworkRoomAifromTrigger or
            GameMsgType.ClaimNetworkRoomObject or GameMsgType.RollRoomEquipment;

        /// <summary>完成表示已入队，不表示对端已收到。单连接队列保证可靠消息顺序。</summary>
        public Task SendMessageAsync(string message)
        {
            if (IsConnected) _sendQueue.Enqueue(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text);
            return Task.CompletedTask;
        }

        /// <summary>完成表示已入队；需要业务确认时使用请求/响应协议。</summary>
        public Task SendBinaryAsync(byte[] data)
        {
            if (IsConnected) _sendQueue.Enqueue(data, WebSocketMessageType.Binary);
            return Task.CompletedTask;
        }

        public void QueueLatestBinary(ulong key, byte[] data, int maxAgeMs = 0)
        {
            if (IsConnected) _sendQueue.Enqueue(data, WebSocketMessageType.Binary, key,
                maxAgeMs: maxAgeMs);
        }

        public void QueueLatestTransformBatch(ulong key, byte[] data)
        {
            if (IsConnected)
                _sendQueue.Enqueue(data, WebSocketMessageType.Binary, key,
                    MergeTransformBatches, maxAgeMs: 250);
        }

        private static byte[] MergeTransformBatches(byte[] previousBytes, byte[] latestBytes)
        {
            Msg previous = Msg.Parser.ParseFrom(previousBytes);
            Msg latest = Msg.Parser.ParseFrom(latestBytes);
            var indexes = new Dictionary<uint, int>(previous.NetworkTransforms.Count);
            for (int i = 0; i < previous.NetworkTransforms.Count; i++)
                indexes[previous.NetworkTransforms[i].ObjectId] = i;
            foreach (NetworkTransformData snapshot in latest.NetworkTransforms)
            {
                if (indexes.TryGetValue(snapshot.ObjectId, out int index))
                    previous.NetworkTransforms[index] = snapshot;
                else
                {
                    indexes[snapshot.ObjectId] = previous.NetworkTransforms.Count;
                    previous.NetworkTransforms.Add(snapshot);
                }
            }
            return previous.ToByteArray();
        }

        public async Task WaitForSnapshotSendCapacityAsync()
        {
            try
            {
                while (IsConnected && _sendQueue.PendingCount >= 128)
                    await Task.Delay(10, _cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        // 房间状态不能跨越 Entered/Load 等消息覆盖，以可靠 FIFO 单路发送。
        public void QueueLatestControlBinary(ulong key, byte[] data)
        {
            if (IsConnected) _sendQueue.Enqueue(data, WebSocketMessageType.Binary);
        }

        private async Task SendQueuedAsync(byte[] data, WebSocketMessageType type, CancellationToken token)
        {
            await _sendLock.WaitAsync(token);
            try
            {
                if (!IsConnected) throw new IOException("socket is not open");
                await _webSocket.SendAsync(new ArraySegment<byte>(data), type, true, token);
            }
            finally { _sendLock.Release(); }
        }

        private void OnSendQueueFailed(Exception exception)
        {
            Console.WriteLine($"[{PlayerId}] Slow/failed connection isolated: {exception.Message}");
            AbortConnection();
        }

        public void AbortConnection()
        {
            _sendQueue.Stop();
            try { _webSocket.Abort(); } catch { }
            _closeTcs.TrySetResult(true);
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        }

        private Task SafeCloseAsync(
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Closing")
        {
            lock (_closeGate)
            {
                if (Interlocked.Exchange(ref _closeStarted, 1) == 1)
                    return _closeTask;
                _closeTask = CloseCoreAsync(closeStatus, statusDescription);
                return _closeTask;
            }
        }

        private async Task CloseCoreAsync(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            _closeTcs.TrySetResult(true);
            try { _cts.Cancel(); } catch { }
            _sendQueue.Stop();

            try
            {
                if (_webSocket.State == WebSocketState.Open ||
                    _webSocket.State == WebSocketState.CloseReceived)
                {
                    using var timeoutCts = new CancellationTokenSource(CloseTimeout);
                    await _sendLock.WaitAsync(timeoutCts.Token);
                    try
                    {
                        await _webSocket.CloseAsync(closeStatus, statusDescription, timeoutCts.Token);
                    }
                    finally { _sendLock.Release(); }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{PlayerId}] Close failed: {ex.GetType().Name}, {ex.Message}, abort socket.");
                try { _webSocket.Abort(); } catch { }
            }
        }

        private void MarkActive()
        {
            LastActiveAt = DateTimeOffset.UtcNow;
        }

        private void QueueOfflineSave()
        {
            if (Interlocked.Exchange(ref _offlineSaved, 1) == 1)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(UserId))
            {
                return;
            }

            OfflineSaveQueue.TryEnqueue(UserId, ServerId);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            if (!string.IsNullOrWhiteSpace(UserId) &&
                Interlocked.CompareExchange(ref _offlineSaved, 1, 0) == 0)
            {
                OfflineSaveQueue.TryEnqueue(UserId, ServerId);
            }

            _cts.Cancel();
            _sendQueue.Stop();
            _requestAbortedRegistration.Dispose();

            try { _webSocket.Dispose(); } catch { }
            _ = DisposeResourcesAsync();
        }

        private async Task DisposeResourcesAsync()
        {
            try { await Task.WhenAll(_sendQueue.Completion, _receiveTask, _commands.Completion); }
            catch (Exception) { }
            // 接收结束不代表 CloseAsync 已完成，不能提前销毁它仍在使用的发送锁。
            Task closing;
            lock (_closeGate) closing = _closeTask;
            try { await closing; } catch (Exception) { }
            _cts.Dispose();
            _sendLock.Dispose();
            _userDataLock.Dispose();
        }
    }
}
