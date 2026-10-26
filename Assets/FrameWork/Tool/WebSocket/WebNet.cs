using System;
using GameData;
using Google.Protobuf;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;


using LitJson;
using UnityEngine;
using UnityWebSocket;

namespace FrameWork.WebSocket
{
    public static class WebNet
    {
        public static Action<object,OpenEventArgs>  OnOpen;
        public static Action<object,CloseEventArgs>  OnClose;
        public static Action<object,MessageEventArgs>  OnMessage;
        public static Action<Msg> OnParsedMessage;
        public static Action<object,ErrorEventArgs>  OnError;
        public static Action  OnServerClose;
        public static Action  OnClientClose;
        public static Action<int, float> OnReconnectScheduled;
        public static Action OnReconnected;
        public static Action<string> OnLoginFailed;
        /// <summary>第一个等待响应的请求开始时触发，用于打开全屏 Loading。</summary>
        public static Action OnRequestLoadingOpen;
        /// <summary>所有等待响应的请求结束时触发，用于关闭全屏 Loading。</summary>
        public static Action OnRequestLoadingClose;
        private static UnityWebSocket.WebSocket _webSocket;
        private static string _pendingLoginUserId;
        private static string _pendingLoginServerId;
        private static string _address;
        private static int _reconnectVersion;
        private static bool _manualDisconnect;
        private static bool _reconnectScheduled;
        private static bool _hasConnectedBefore;
        private static int _reconnectAttempt;
        private static int _connectionWatchVersion;
        private static int _loginWatchVersion;
        private static bool _loginAwaitingConfirmation;
        private const string ResponseErrorCodeKey = "__web_response_error_code";

        private static async UniTaskVoid WatchConnectionAsync(UnityWebSocket.WebSocket socket, int version)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(15), ignoreTimeScale: true);
            if (!_manualDisconnect && version == _connectionWatchVersion && ReferenceEquals(socket, _webSocket))
                RetryConnection("连接超时");
        }

        private static async UniTaskVoid WatchLoginAsync(UnityWebSocket.WebSocket socket, int version)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(30), ignoreTimeScale: true);
            if (!_manualDisconnect && version == _loginWatchVersion && ReferenceEquals(socket, _webSocket))
                RetryConnection("登录确认超时");
        }

        private static void RetryConnection(string reason)
        {
            if (_manualDisconnect) return;
            Debug.LogWarning($"[WebNet] {reason}，安排重试");
            ReleaseSocket();
            FailPendingRequests(new InvalidOperationException(reason));
            ScheduleReconnect();
        }

        private static void ReleaseSocket()
        {
            ++_connectionWatchVersion;
            ++_loginWatchVersion;
            var oldSocket = _webSocket;
            DetachSocketEvents();
            _webSocket = null;
            try { oldSocket?.CloseAsync(); }
            catch (Exception exception) { Debug.LogWarning(exception.Message); }
        }
        private const string RequestIdKey = "__web_request_id";
        private const string ResponseKey = "__web_response";
        private const string ResponseErrorKey = "__web_response_error";
        private const string ServerVersionQueryKey = "__server_version_query";
        private const string LatencyProbeKey = "__latency_probe";
        private const string ShowRewardPanelKey = "__show_reward_panel";
        private static readonly Dictionary<string, UniTaskCompletionSource<Msg>> PendingRequests = new();
        private static readonly Dictionary<string, UniTaskCompletionSource<Msg>> PendingLatencyRequests = new();
        public readonly struct ServerLatencySample
        {
            public readonly float RttMs;
            public readonly int ServerQueuePackets;
            public readonly int ServerQueueBytes;
            public readonly int ServerQueueWaitMs;
            public readonly int ServerWriteMs;
            public readonly long ExpiredRealtimePackets;

            public ServerLatencySample(float rttMs, Msg response)
            {
                RttMs = rttMs;
                ServerQueuePackets = ReadInt(response, "__server_queue_packets");
                ServerQueueBytes = ReadInt(response, "__server_queue_bytes");
                ServerQueueWaitMs = ReadInt(response, "__server_queue_wait_ms");
                ServerWriteMs = ReadInt(response, "__server_write_ms");
                ExpiredRealtimePackets = response.DataDic.TryGetValue("__server_expired_realtime", out string value) &&
                    long.TryParse(value, out long count) ? count : 0;
            }

            static int ReadInt(Msg response, string key) =>
                response.DataDic.TryGetValue(key, out string value) && int.TryParse(value, out int result)
                    ? result : 0;
        }
        private static int _requestLoadingCount;
        public static string CurrentUserId { get; private set; }
        public static bool IsConnected => _webSocket != null && _webSocket.ReadyState == WebSocketState.Open;
        public static int PendingSendCount => _webSocket?.PendingSendCount ?? 0;
        public static int BufferedSendBytes => _webSocket?.BufferedSendBytes ?? 0;
        public static int ConnectionGeneration => _connectionWatchVersion;
        public static string ServerAddress => _address;
        public static bool IsReconnecting => _reconnectScheduled;
        public static bool IsRequestLoading => _requestLoadingCount > 0;

        /// <summary>
        /// 注册请求 Loading 界面的打开和关闭方法。重复调用会替换之前注册的方法。
        /// </summary>
        public static void SetRequestLoadingHandlers(Action openLoading, Action closeLoading)
        {
            OnRequestLoadingOpen = openLoading;
            OnRequestLoadingClose = closeLoading;
        }

        /// <summary>
        /// 增加一个 Loading 占用。业务代码手动调用时，必须与 CloseRequestLoading 成对使用。
        /// </summary>
        public static void OpenRequestLoading()
        {
            _requestLoadingCount++;
            if (_requestLoadingCount == 1)
                InvokeLoadingHandler(OnRequestLoadingOpen);
        }

        /// <summary>释放一个 Loading 占用；仅当全部请求都结束后才真正关闭界面。</summary>
        public static void CloseRequestLoading()
        {
            if (_requestLoadingCount <= 0)
            {
                _requestLoadingCount = 0;
                return;
            }

            _requestLoadingCount--;
            if (_requestLoadingCount == 0)
                InvokeLoadingHandler(OnRequestLoadingClose);
        }

        private static void InvokeLoadingHandler(Action handler)
        {
            if (handler == null)
                return;
            try
            {
                handler.Invoke();
            }
            catch (Exception exception)
            {
                // Loading 属于表现层，不能因为界面异常阻断网络请求或清理流程。
                Debug.LogException(exception);
            }
        }
        
        
       // private static string _address="ws://159.75.220.92:5100";
       
        public static void Connect()
        {
#if Debug
            Connect(Config.DebugIp);
#else
            Connect(Config.ServerIp);
#endif
            
        }
        
        public static void Connect(string address)
        {
            Debug.Log($"链接到服务器:{address}");
            if (string.IsNullOrWhiteSpace(address))
            {
                Debug.LogError("WebSocket 地址不能为空。");
                return;
            }

            if (IsConnected || (_webSocket != null && _webSocket.ReadyState == WebSocketState.Connecting))
                return;

            _address = address.Trim();
            _manualDisconnect = false;
            CancelReconnect();
            CreateSocketAndConnect();
        }

        private static void CreateSocketAndConnect()
        {
            ReleaseSocket();
            try
            {
            _webSocket = new UnityWebSocket.WebSocket(_address);
            _webSocket.OnOpen += OnOpenCall;
            _webSocket.OnClose += OnCloseCall;
            _webSocket.OnMessage += OnMessageCall;
            _webSocket.OnError += OnErrorCall;
            WatchConnectionAsync(_webSocket, ++_connectionWatchVersion).Forget();
            _webSocket.ConnectAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                RetryConnection("创建连接失败");
            }
        }
        
        public static void Disconnect()
        {
            _manualDisconnect = true;
            ++_connectionWatchVersion;
            ++_loginWatchVersion;
            CancelReconnect();
            _webSocket?.CloseAsync();
        }

        private static void OnErrorCall(object sender, ErrorEventArgs e)
        {
            if (!ReferenceEquals(sender, _webSocket)) return;
            try { OnError?.Invoke(sender, e); }
            finally
            {
                if (!_manualDisconnect) ScheduleReconnect();
            }
        }

        private static void OnMessageCall(object sender, MessageEventArgs e)
        {
            if (!ReferenceEquals(sender, _webSocket)) return;
            Msg msg = null;
            if (!e.IsText)
            {
                try { msg = Msg.Parser.ParseFrom(e.RawData); }
                catch (InvalidProtocolBufferException exception) { Debug.LogException(exception); return; }
                if (msg.MsgType == ProtobufMsgType.Server && msg.DataDic.ContainsKey(ServerVersionQueryKey))
                {
                    // 版本响应只唤醒对应查询，不作为 Tips/登录/房间事件广播。
                    CompleteLatencyResponse(msg);
                    CompleteResponse(msg);
                    return;
                }
            }
            if (OnMessage != null)
                foreach (Action<object, MessageEventArgs> handler in OnMessage.GetInvocationList())
                    try { handler(sender, e); } catch (Exception exception) { Debug.LogException(exception); }
            if (msg != null)
                try { OnParsedMessage?.Invoke(msg); }
                catch (Exception exception) { Debug.LogException(exception); }
            // 高频 Protobuf 二进制同步包不逐条输出，避免字符串转换和 Console 开销。
            if (e.IsText)
                Debug.Log(e.Data);

            if (e.IsText) return;
            try
            {
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
            if (msg.MsgType == ProtobufMsgType.Server && msg.ServerMsgType == ServerMsgType.Tips &&
                msg.DataDic.ContainsKey("__wechat_session_invalid"))
            {
                WeChatPlatformMrg.Instance.RetryExpiredSession(_pendingLoginServerId);
                return;
            }
#endif
            if (msg.MsgType != ProtobufMsgType.Game)
            {
                if (msg.ServerMsgType == ServerMsgType.Tips && _loginAwaitingConfirmation &&
                    msg.DataDic.TryGetValue(ResponseErrorCodeKey, out string loginErrorCode) &&
                    (loginErrorCode.Contains("LOGIN", StringComparison.OrdinalIgnoreCase) ||
                     loginErrorCode.Contains("AUTH", StringComparison.OrdinalIgnoreCase) ||
                     loginErrorCode.Contains("WECHAT", StringComparison.OrdinalIgnoreCase) ||
                     loginErrorCode == "INVALID_PLAYER_ID" || loginErrorCode == "DATABASE_UNAVAILABLE"))
                {
                    _loginAwaitingConfirmation = false;
                    ++_loginWatchVersion;
                    OnLoginFailed?.Invoke(string.IsNullOrWhiteSpace(msg.TipsSrt) ? loginErrorCode : msg.TipsSrt);
                }
                switch (msg.ServerMsgType)
                {
                    case ServerMsgType.LoginSuc:
                        _loginAwaitingConfirmation = false;
                        ++_loginWatchVersion;
                        _reconnectAttempt = 0;
                        Debug.Log("[WebNet] 服务器登录确认成功");
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
                        msg.DataDic.TryGetValue("__wechat_session", out string sessionToken);
                        WeChatPlatformMrg.Instance.CacheSession(msg.UserData?.UserId, sessionToken);
#endif
                        // 微信登录发送的是临时 code，真正的玩家 ID 以服务端换取的 openid 为准。
                        if (!string.IsNullOrWhiteSpace(msg.UserData?.UserId))
                            CurrentUserId = msg.UserData.UserId;
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.UserData);
                        break;
                    case ServerMsgType.GetServerListSuc:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.ServerData);
                        break;
                    case ServerMsgType.GetGongGaoSuc:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.GongGaoData);
                        break;
                    case ServerMsgType.UpdateEmail:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.EmailList);
                        break;
                    case ServerMsgType.UpdateUserData:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.UserData);
                        break;
                    case ServerMsgType.UpdateFriendList:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.FriendList);
                        break;
                    case ServerMsgType.FriendMessageReceived:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.FriendMessage);
                        break;
                    // 仅作为 SendFriendMessageAsync 的请求确认，不再派发聊天 UI 回调。
                    case ServerMsgType.FriendMessageSent:
                        break;
                    case ServerMsgType.ChannelChatMessageReceived:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.ChannelChatMessage);
                        break;
                    // 仅作为 SendChannelChatMessageAsync 的请求确认，不再派发聊天 UI 回调。
                    case ServerMsgType.ChannelChatMessageSent:
                        break;
                    case ServerMsgType.GetRankSuc:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg.RankData);
                        break;
                    default:
                        EventMrg.Trigger(msg.MsgType,msg.ServerMsgType,msg);
                        break;
                }
            }

            }
            catch (Exception exception) { Debug.LogException(exception); }

            // 先分发服务器事件，再唤醒请求调用方，避免同步 continuation 抛错阻断事件回调。
            CompleteResponse(msg);
        }

        private static void CompleteResponse(Msg msg)
        {
            if (msg.DataDic.TryGetValue(ResponseKey, out string responseId) &&
                !string.IsNullOrWhiteSpace(responseId) &&
                PendingRequests.TryGetValue(responseId, out UniTaskCompletionSource<Msg> completion))
            {
                if (msg.DataDic.TryGetValue(ResponseErrorKey, out string error) &&
                    !string.IsNullOrWhiteSpace(error))
                    completion.TrySetException(new InvalidOperationException(error));
                else
                    completion.TrySetResult(msg);
            }
        }

        private static void CompleteLatencyResponse(Msg msg)
        {
            if (msg.DataDic.TryGetValue(ResponseKey, out string responseId) &&
                PendingLatencyRequests.TryGetValue(responseId, out var completion))
                completion.TrySetResult(msg);
        }

        private static void OnCloseCall(object sender, CloseEventArgs e)
        {
            if (!ReferenceEquals(sender, _webSocket))
                return;

            ++_connectionWatchVersion;
            ++_loginWatchVersion;
            _loginAwaitingConfirmation = false;

            // 明确的账号替换不是弱网断线，不自动抢回账号。
            if ((int)e.StatusCode == 4001) { _manualDisconnect = true; CancelReconnect(); }
            try
            {
            if (OnClose != null)
                foreach (Action<object, CloseEventArgs> handler in OnClose.GetInvocationList())
                    try { handler(sender, e); } catch (Exception exception) { Debug.LogException(exception); }
            FailPendingRequests(new InvalidOperationException("网络连接已断开，请求未完成。"));

            switch (e.StatusCode)
            {
                case CloseStatusCode.ServerError:
                    OnServerCloseCall();
                    break;
                default:
                    OnClientCloseCall();
                    break;
            }

            }
            finally
            {
                if (!_manualDisconnect) ScheduleReconnect();
            }
        }

        private static async void ScheduleReconnect()
        {
            if (_reconnectScheduled || _manualDisconnect || string.IsNullOrWhiteSpace(_address))
                return;

            _reconnectScheduled = true;
            int version = ++_reconnectVersion;
            int attempt = ++_reconnectAttempt;
            float delaySeconds = Mathf.Min(30f, Mathf.Pow(2f, Mathf.Min(attempt - 1, 5)));
            try { OnReconnectScheduled?.Invoke(attempt, delaySeconds); }
            catch (Exception exception) { Debug.LogException(exception); }
            Debug.Log($"网络断开，{delaySeconds:0} 秒后进行第 {attempt} 次重连。");

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), ignoreTimeScale: true);
                if (version == _reconnectVersion && !_manualDisconnect)
                {
                    // 释放本次调度占位，新连接即使立即失败也能安排下一次重试。
                    CancelReconnect();
                    CreateSocketAndConnect();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                // 旧任务结束时不能清掉新任务的调度状态。
                if (version == _reconnectVersion)
                    CancelReconnect();
            }
        }

        private static void CancelReconnect()
        {
            // 微信 IL2CPP 日志确认 CTS.Dispose 会触发 function signature mismatch。
            // 旧等待自然结束后检查代次，不再取消/释放计时器。
            ++_reconnectVersion;
            _reconnectScheduled = false;
        }

        private static void DetachSocketEvents()
        {
            if (_webSocket == null) return;
            _webSocket.OnOpen -= OnOpenCall;
            _webSocket.OnClose -= OnCloseCall;
            _webSocket.OnMessage -= OnMessageCall;
            _webSocket.OnError -= OnErrorCall;
        }

        private static void OnServerCloseCall()
        {
            OnServerClose?.Invoke();
            Debug.Log("服务器关闭断开链接。。。");
        }
        
        private static void OnClientCloseCall()
        {
            OnClientClose?.Invoke();
            Debug.Log("客户端断开链接。。。");
        }

        private static void OnOpenCall(object sender, OpenEventArgs e)
        {
            if (!ReferenceEquals(sender, _webSocket)) return;
            bool reconnected = _hasConnectedBefore;
            _hasConnectedBefore = true;
            ++_connectionWatchVersion;
            CancelReconnect();
            try { OnOpen?.Invoke(sender, e); }
            catch (Exception exception) { Debug.LogException(exception); }
            Debug.Log("链接成功");
            if (reconnected && (!string.IsNullOrWhiteSpace(_pendingLoginUserId) ||
                                !string.IsNullOrWhiteSpace(CurrentUserId)))
            {
                // 只恢复连接，不恢复登录。旧会话和异步登录不能带入新连接。
                ++_loginWatchVersion;
                _loginAwaitingConfirmation = false;
                _pendingLoginUserId = null;
                _pendingLoginServerId = null;
                CurrentUserId = null;
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
                WeChatPlatformMrg.Instance.ClearLoginSession();
#endif
                if (FrameWork.Script.WebNet.WebNetworkRoomManager.Instance != null)
                    FrameWork.Script.WebNet.WebNetworkRoomManager.Instance.ReturnToStartScene();
                else if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != FrameWork.Script.WebNet.WebNetworkIntegration.LoginSceneName)
                    ABMrg.LoadSceneAsync(FrameWork.Script.WebNet.WebNetworkIntegration.LoginSceneName);
                OnLoginFailed?.Invoke("连接已恢复，请重新登录。");
            }

            if (reconnected)
            {
                GetServerList();
                OnReconnected?.Invoke();
            }
        }

        /// <summary>前台恢复时废弃可能已失效的连接，不改变用户主动断开的状态。</summary>
        public static void ReconnectAfterResume()
        {
            if (_manualDisconnect || string.IsNullOrWhiteSpace(_address))
            {
                Debug.Log($"[WebNet] 跳过前台重连：主动断开={_manualDisconnect}，已配置地址={!string.IsNullOrWhiteSpace(_address)}");
                return;
            }
            Debug.Log("[WebNet] 前台恢复：清理旧连接并开始重连");
            CancelReconnect();
            ReleaseSocket();
            FailPendingRequests(new InvalidOperationException("正在恢复网络连接，请重试。"));
            CreateSocketAndConnect();
        }


        public static void Send(byte[] data)
        {
            if (IsConnected && data != null)
                _webSocket.SendAsync(data);
        }
        
        public static void Send(IMessage data)
        {
            if (IsConnected && data != null)
                _webSocket.SendAsync(data.ToByteArray());
        }

        /// <summary>可丢帧的实时状态只保留同一对象尚未发出的最新一帧。</summary>
        public static void SendLatest(IMessage data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            if (IsConnected && data != null)
                _webSocket.SendLatestAsync(data.ToByteArray(), key, merge, maxAgeMs);
        }
        public static void FlushRealtime() => _webSocket?.FlushLatest();
        /// <summary>
        /// 发送一次请求并等待服务端使用 ReplyAsync/ReplyErrorAsync 返回对应结果。
        /// 不适用于 Transform、动画、RPC 等持续广播消息。
        /// </summary>
        public static async UniTask<Msg> RequestAsync(Msg request, float timeoutSeconds = 10f)
        {
            if (!IsConnected)
                throw new InvalidOperationException("尚未连接到服务器。");
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string requestId = Guid.NewGuid().ToString("N");
            request.MsgType = ProtobufMsgType.Game;
            request.DataDic[RequestIdKey] = requestId;
            var completion = new UniTaskCompletionSource<Msg>();
            PendingRequests.Add(requestId, completion);
            OpenRequestLoading();
            try
            {
                Send(request);
                return await completion.Task.Timeout(
                    TimeSpan.FromSeconds(Mathf.Max(0.1f, timeoutSeconds)));
            }
            finally
            {
                PendingRequests.Remove(requestId);
                CloseRequestLoading();
            }
        }

        public static async UniTask<T> RequestAsync<T>(
            Msg request, Func<Msg, T> resultSelector, float timeoutSeconds = 10f)
        {
            if (resultSelector == null)
                throw new ArgumentNullException(nameof(resultSelector));
            try
            {
                Msg response = await RequestAsync(request, timeoutSeconds);
                return resultSelector(response);
            }
            catch (WebRequestException exception)
            {
                await FrameWork.Script.WebNet.WebNetworkIntegration.ShowErrorAsync($"错误码：{exception.ErrorCode}--{exception.Message}");
                // 完整的服务器响应
                // Msg response = exception.Response;
            }
            catch (TimeoutException)
            {
                await FrameWork.Script.WebNet.WebNetworkIntegration.ShowErrorAsync($"请求超时，服务器没有及时响应");
            }
            catch (InvalidOperationException exception)
            {
                await FrameWork.Script.WebNet.WebNetworkIntegration.ShowErrorAsync($"连接异常：{exception.Message}");
            }
            return default;
        }

        public static async UniTask<UserData> LoginAsync(
            string userId, string serverId, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("userId 和 serverId 不能为空。");
            CurrentUserId = userId;
            _pendingLoginUserId = userId;
            _pendingLoginServerId = serverId;
            var request = new Msg
            {
                GameMsgType = GameMsgType.Login,
                Id = userId,
                ServerId = serverId
            };
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
            request.DataDic["__login_provider"] = "wechat";
#endif
            Msg response = await RequestAsync(request, timeoutSeconds);
            if (!string.IsNullOrWhiteSpace(response.UserData?.UserId))
                CurrentUserId = response.UserData.UserId;
            return response.UserData;
        }

        public static async UniTask<ServerDataList> GetServerListAsync(float timeoutSeconds = 10f)
        {
            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.GetServerList
            }, timeoutSeconds);
            return response.ServerData;
        }

        public static async UniTask<GongGaoData> GetGongGaoAsync(float timeoutSeconds = 10f)
        {
            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.GetGongGao
            }, timeoutSeconds);
            return response.GongGaoData;
        }

        /// <summary>更新当前登录玩家的头像链接；传空字符串可清除头像。</summary>
        /// <param name="avatarUrl">HTTP/HTTPS 头像链接，或用于清除头像的空字符串。</param>
        /// <param name="timeoutSeconds">等待服务端响应的超时时间（秒）。</param>
        /// <returns>服务端保存后的完整玩家数据。</returns>
        public static async UniTask<UserData> SetAvatarAsync(
            string avatarUrl, float timeoutSeconds = 10f)
        {
            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.SetAvatar,
                UserData = new UserData { Avatar = avatarUrl?.Trim() ?? string.Empty }
            }, timeoutSeconds);
            return response.UserData;
        }

        /// <summary>新增或修改当前登录玩家 DataDic 中的一项数据。</summary>
        /// <param name="key">字典键，不能为空。</param>
        /// <param name="value">字典值；传入 null 时按空字符串保存。</param>
        /// <param name="timeoutSeconds">等待服务端响应的超时时间（秒）。</param>
        /// <returns>服务端保存后的完整玩家数据。</returns>
        public static UniTask<UserData> SetUserDataDicAsync(
            string key, string value, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("DataDic 的 key 不能为空。", nameof(key));

            return SetUserDataDicAsync(new Dictionary<string, string>
            {
                [key.Trim()] = value ?? string.Empty
            }, timeoutSeconds);
        }

        /// <summary>批量新增或修改当前登录玩家 DataDic；已存在的键会被覆盖。</summary>
        /// <param name="values">需要保存的键值对。</param>
        /// <param name="timeoutSeconds">等待服务端响应的超时时间（秒）。</param>
        /// <returns>服务端保存后的完整玩家数据。</returns>
        public static async UniTask<UserData> SetUserDataDicAsync(
            IReadOnlyDictionary<string, string> values, float timeoutSeconds = 10f)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count == 0)
                throw new ArgumentException("DataDic 数据不能为空。", nameof(values));

            var request = new Msg { GameMsgType = GameMsgType.AddUserDataDic };
            foreach (var pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("DataDic 的 key 不能为空。", nameof(values));
                request.DataDic[pair.Key.Trim()] = pair.Value ?? string.Empty;
            }

            Msg response = await RequestAsync(request, timeoutSeconds);
            return response.UserData;
        }

        /// <summary>在当前区服直接建立双向好友关系。</summary>
        /// <param name="friendUserId">目标玩家 ID。</param>
        /// <param name="timeoutSeconds">等待服务端响应的超时时间（秒）。</param>
        /// <returns>添加成功后的最新好友列表。</returns>
        public static async UniTask<FriendListData> AddFriendAsync(
            string friendUserId, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrWhiteSpace(friendUserId))
                throw new ArgumentException("好友玩家 ID 不能为空。", nameof(friendUserId));
            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.AddFriend,
                Id = friendUserId.Trim()
            }, timeoutSeconds);
            return response.FriendList;
        }

        /// <summary>解除当前玩家与目标玩家之间的双向好友关系。</summary>
        /// <param name="friendUserId">需要删除的好友玩家 ID。</param>
        /// <param name="timeoutSeconds">等待服务端响应的超时时间（秒）。</param>
        /// <returns>删除成功后的最新好友列表。</returns>
        public static async UniTask<FriendListData> DeleteFriendAsync(
            string friendUserId, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrWhiteSpace(friendUserId))
                throw new ArgumentException("好友玩家 ID 不能为空。", nameof(friendUserId));
            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.DeleteFriend,
                Id = friendUserId.Trim()
            }, timeoutSeconds);
            return response.FriendList;
        }

        /// <summary>获取当前玩家的好友列表；服务端会标记在线状态并将在线好友排在前面。</summary>
        /// <param name="timeoutSeconds">等待服务端响应的超时时间（秒）。</param>
        /// <returns>包含好友公开资料和在线状态的列表。</returns>
        public static async UniTask<FriendListData> GetFriendListAsync(float timeoutSeconds = 10f)
        {
            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.GetFriendList
            }, timeoutSeconds);
            return response.FriendList;
        }

        /// <summary>向一名在线好友发送实时私聊消息。</summary>
        /// <param name="receiverUserId">接收消息的好友玩家 ID。</param>
        /// <param name="content">消息正文；服务端限制为 1 到 500 个字符。</param>
        /// <param name="timeoutSeconds">等待送达确认的超时时间（秒）。</param>
        /// <returns>包含服务端消息 ID 和发送时间的送达确认。</returns>
        public static async UniTask<FriendMessageData> SendFriendMessageAsync(
            string receiverUserId, string content, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrWhiteSpace(receiverUserId))
                throw new ArgumentException("消息接收者不能为空。", nameof(receiverUserId));
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("消息内容不能为空。", nameof(content));

            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.SendFriendMessage,
                FriendMessage = new FriendMessageData
                {
                    ReceiverUserId = receiverUserId.Trim(),
                    Content = content.Trim()
                }
            }, timeoutSeconds);
            return response.FriendMessage;
        }

        /// <summary>向当前逻辑区服内的所有在线玩家发送聊天消息。</summary>
        public static UniTask<ChannelChatMessageData> SendServerChatMessageAsync(
            string content, float timeoutSeconds = 10f)
        {
            return SendChannelChatMessageAsync(ChatChannelType.Server, content, timeoutSeconds);
        }

        /// <summary>向当前游戏房间内的玩家发送聊天消息；大厅不属于游戏房间。</summary>
        public static UniTask<ChannelChatMessageData> SendRoomChatMessageAsync(
            string content, float timeoutSeconds = 10f)
        {
            return SendChannelChatMessageAsync(ChatChannelType.Room, content, timeoutSeconds);
        }

        private static async UniTask<ChannelChatMessageData> SendChannelChatMessageAsync(
            ChatChannelType channel, string content, float timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("聊天内容不能为空。", nameof(content));
            if (content.Trim().Length > 200)
                throw new ArgumentException("聊天内容不能超过 200 个字符。", nameof(content));

            Msg response = await RequestAsync(new Msg
            {
                GameMsgType = GameMsgType.SendChannelChatMessage,
                ChannelChatMessage = new ChannelChatMessageData
                {
                    Channel = channel,
                    Content = content.Trim()
                }
            }, timeoutSeconds);
            return response.ChannelChatMessage;
        }

        private static void FailPendingRequests(Exception exception)
        {
            // TrySetException 可同步恢复 RequestAsync，其 finally 会删除字典元素。
            // 先复制并清空，再完成请求，避免枚举失效使前台重连在创建 socket 前中断。
            var completions = new List<UniTaskCompletionSource<Msg>>(PendingRequests.Values);
            PendingRequests.Clear();
            foreach (UniTaskCompletionSource<Msg> completion in completions)
                completion.TrySetException(exception);
            var latencyCompletions = new List<UniTaskCompletionSource<Msg>>(PendingLatencyRequests.Values);
            PendingLatencyRequests.Clear();
            foreach (UniTaskCompletionSource<Msg> completion in latencyCompletions)
                completion.TrySetException(exception);
        }

        /// <summary>通过当前游戏 WebSocket 做轻量请求往返测试，不显示请求 Loading。</summary>
        public static async UniTask<float> MeasureServerRttAsync(float timeoutSeconds = 3f)
        {
            return (await MeasureServerLatencyAsync(timeoutSeconds)).RttMs;
        }

        /// <summary>往返时间与服务端最近一次发送队列状态；队列数据来自上一条已发送消息。</summary>
        public static async UniTask<ServerLatencySample> MeasureServerLatencyAsync(float timeoutSeconds = 3f)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket 未连接。");
            var socket = _webSocket;
            int generation = ConnectionGeneration;
            string requestId = Guid.NewGuid().ToString("N");
            var completion = new UniTaskCompletionSource<Msg>();
            PendingLatencyRequests.Add(requestId, completion);
            var request = new Msg { MsgType = ProtobufMsgType.Game, GameMsgType = GameMsgType.GetServerList };
            request.DataDic[ServerVersionQueryKey] = "1";
            request.DataDic[LatencyProbeKey] = "1";
            request.DataDic[RequestIdKey] = requestId;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            try
            {
                Send(request);
                Msg response = await completion.Task.Timeout(TimeSpan.FromSeconds(Mathf.Max(0.1f, timeoutSeconds)));
                if (!IsConnected || !ReferenceEquals(socket, _webSocket) || generation != ConnectionGeneration)
                    throw new InvalidOperationException("测量期间连接已变化。");
                return new ServerLatencySample(
                    (float)((Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0), response);
            }
            finally
            {
                PendingLatencyRequests.Remove(requestId);
            }
        }

        public static async UniTask<string> GetServerVersionAsync()
        {
            if (!IsConnected || _manualDisconnect)
                throw new InvalidOperationException("WebSocket 未连接，无法校验服务器版本。");
            var socket = _webSocket;
            int generation = ConnectionGeneration;
            var request = new Msg { GameMsgType = GameMsgType.GetServerList };
            request.DataDic[ServerVersionQueryKey] = "1";
            var response = await RequestAsync(request, 10f);
            if (!IsConnected || _manualDisconnect || !ReferenceEquals(socket, _webSocket) || generation != ConnectionGeneration)
                throw new InvalidOperationException("版本校验期间连接已变化，请重新检测。");
            if (!response.DataDic.ContainsKey(ServerVersionQueryKey) ||
                !response.DataDic.TryGetValue("__server_version", out string version) || string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("服务器未返回版本，请部署支持 WebSocket 版本校验的服务端。");
            return version.Trim();
        }

        public static void Send(string data)
        {
            if (IsConnected && data != null)
                _webSocket.SendAsync(data);
        }

        public static void Login(string userId,string serverId)
        {
            if (string.IsNullOrWhiteSpace(userId)|| string.IsNullOrWhiteSpace(serverId))
                return;
            CurrentUserId = userId;
            _pendingLoginUserId = userId;
            _pendingLoginServerId = serverId;
            if (IsConnected)
                SendLogin(userId,serverId);
        }

        public static void RequestEmail()
        {
            Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.GetEmail
            });
        }

        /// <summary>请求服务器给当前玩家增加一种道具。</summary>
        public static void AddItem(string itemKey, long count, bool showRewardPanel = false)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(itemKey) || count == 0)
                return;

            string path = itemKey.StartsWith("Item.", StringComparison.Ordinal)
                ? itemKey
                : $"Item.{itemKey}";
            var msg = new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.AddItem
            };
            msg.AddItemData[path] = count;
            msg.DataDic[ShowRewardPanelKey] = showRewardPanel.ToString();
            Send(msg);
        }

        /// <summary>请求服务器批量增加道具，字典 Key 不需要 Item. 前缀。</summary>
        public static void AddItems(
            IReadOnlyDictionary<string, long> items, bool showRewardPanel = false)
        {
            if (!IsConnected || items == null || items.Count == 0)
                return;

            var msg = new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.AddItem
            };
            foreach (KeyValuePair<string, long> item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value == 0)
                    continue;
                string path = item.Key.StartsWith("Item.", StringComparison.Ordinal)
                    ? item.Key
                    : $"Item.{item.Key}";
                msg.AddItemData[path] = item.Value;
            }
            if (msg.AddItemData.Count > 0)
            {
                msg.DataDic[ShowRewardPanelKey] = showRewardPanel.ToString();
                Send(msg);
            }
        }

        /// <summary>请求服务器把装备加入背包，装备 ID 由服务器生成。</summary>
        public static void AddEquip(object equip, bool showRewardPanel = false)
        {
            if (!IsConnected || equip == null || string.IsNullOrWhiteSpace(equip.GetType().GetField("equipKey")?.GetValue(equip) as string))
                return;

            var msg = new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.AddEquip
            };
            msg.RewardEquip.Add(new EquipData
            {
                Data = ByteString.CopyFromUtf8(JsonMapper.ToJson(equip)),
                Id = equip.GetType().GetField("id")?.GetValue(equip) as string ?? string.Empty
            });
            msg.DataDic[ShowRewardPanelKey] = showRewardPanel.ToString();
            Send(msg);
        }

        /// <summary>按服务端存储 ID 批量删除未穿戴装备，等待保存后的玩家数据。</summary>
        public static async UniTask<UserData> DeleteEquipsAsync(IEnumerable<string> equipIds)
        {
            if (equipIds == null)
                throw new ArgumentNullException(nameof(equipIds));
            var request = new Msg { GameMsgType = GameMsgType.DelEquip };
            request.EquipIds.AddRange(new HashSet<string>(equipIds, StringComparer.Ordinal));
            Msg response = await RequestAsync(request);
            return response.UserData;
        }

        private static void SendLogin(string userId,string serverId)
        {
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
            SendVersionCheckedLoginAsync(userId, serverId).Forget(exception =>
            {
                Debug.LogException(exception);
                OnLoginFailed?.Invoke("版本检测失败，请重试。");
            });
#else
            SendLoginCore(userId, serverId);
#endif
        }

#if WX_MINIGAME_BUILD && !UNITY_EDITOR
        private static async UniTask SendVersionCheckedLoginAsync(string userId, string serverId)
        {
            var socket = _webSocket;
            int attempt = ++_loginWatchVersion;
            var platform = WeChatPlatformMrg.Instance;
            bool passed = await platform.CheckClientVersionAsync();
            if (!passed)
            {
                platform.ShowVersionError();
                OnLoginFailed?.Invoke(platform.VersionError);
                return;
            }
            if (attempt != _loginWatchVersion || !ReferenceEquals(socket, _webSocket) || !IsConnected || _manualDisconnect) return;
            SendLoginCore(userId, serverId);
        }
#endif

        private static void SendLoginCore(string userId,string serverId)
        {
            _loginAwaitingConfirmation = true;
            WatchLoginAsync(_webSocket, ++_loginWatchVersion).Forget();
            var msg = new Msg();
            msg.MsgType = ProtobufMsgType.Game;
            msg.GameMsgType = GameMsgType.Login;
            msg.Id=userId;
            msg.ServerId = serverId;
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
            msg.DataDic["__login_provider"] = "wechat";
            if (WeChatPlatformMrg.Instance.TryGetSession(userId, out string sessionToken))
                msg.DataDic["__wechat_session"] = sessionToken;
#endif
            Send(msg);
        }

        public static void GetGongGao()
        {
            Send(new Msg()
            {
                MsgType =  ProtobufMsgType.Game,
                GameMsgType = GameMsgType.GetGongGao
            });
        }
        
        public static void GetServerList()
        {
            if (!IsConnected)return;
            var msg = new Msg()
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.GetServerList
            };
            Send(msg);
        }

        /// <summary>
        /// 获取邮件奖励
        /// </summary>
        /// <param name="ids"></param>
        public static void GetEmailsReward(string[] ids)
        {
            var msg=new Msg()
            {
                EmailIds = { ids },
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.GetEmailReward
            };
            Send(msg.ToByteArray());
        }


        public static void GetRank(RankType rankType=RankType.Nor)
        {
            var msg=new Msg()
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.GetRank,
                RankName = rankType.ToString()
            };
            Send(msg.ToByteArray());
        }

        public static void UpdateRank(RankData rankData, RankType rankType = RankType.Nor)
        {
            var msg=new Msg()
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UpdateRank,
                RankName = rankType.ToString(),
                UpdateRankData = rankData
            };
            Debug.Log($"更新排行榜:{msg}");
            Send(msg.ToByteArray());
        }
    }
}
