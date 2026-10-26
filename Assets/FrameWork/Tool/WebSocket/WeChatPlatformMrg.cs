#if WX_MINIGAME_BUILD && !UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using WeChatWASM;

namespace FrameWork.WebSocket
{
    /// <summary>微信平台登录和前后台生命周期入口，首次使用时创建并跨场景保留。</summary>
    public sealed class WeChatPlatformMrg : MonoBehaviour
    {
        private static WeChatPlatformMrg _instance;
        private bool _hidden;
        private string _openId;
        private string _sessionToken;
        private int _loginAttempt;
        private WXUpdateManager _updateManager;
        private bool _updateReady;
        private bool _updateFailed;
        private bool _updateRequired;
        private bool _applyingUpdate;
        private bool _versionCheckRunning;
        private bool _versionCheckPassed;
        public string VersionError { get; private set; }

        // 只在服务端明确要求更新时应用新包，避免游戏过程中突然重启。
        private void ApplyRequiredUpdate()
        {
            if (!_updateRequired || !_updateReady || _applyingUpdate) return;
            _applyingUpdate = true;
            WebNet.Disconnect();
            try { _updateManager.ApplyUpdate(); }
            catch (Exception exception)
            {
                _applyingUpdate = false;
                Debug.LogException(exception);
            }
        }

        /// <summary>只在已有连接上查询和比较版本，不负责建立连接或重连。</summary>
        public async UniTask<bool> CheckClientVersionAsync()
        {
            if (_versionCheckRunning)
            {
                await UniTask.WaitUntil(() => !_versionCheckRunning);
                return _versionCheckPassed;
            }
            _versionCheckRunning = true;
            _versionCheckPassed = false;
            try
            {
                string serverVersion = await WebNet.GetServerVersionAsync();
                if (string.IsNullOrWhiteSpace(Config.ServerVersion))
                    throw new InvalidOperationException("客户端未配置兼容的服务器版本。");
                _updateRequired = ServerVersionPolicy.RequiresUpdate(Config.ServerVersion, serverVersion);
                if (!_updateRequired)
                {
                    VersionError = null;
                    _versionCheckPassed = true;
                    return true;
                }
                // 只有客户端低于服务端要求时关闭连接，较新的客户端可以正常登录。
                WebNet.Disconnect();
                ApplyRequiredUpdate();
                VersionError = _updateFailed
                    ? "新版本下载失败，请检查网络后关闭并重新打开小游戏。"
                    : $"客户端服务器兼容版本 {Config.ServerVersion} 低于服务端要求 {serverVersion}，需要更新小游戏。新包下载完成后将自动重启；若长时间未更新，请关闭并重新打开小游戏。";
                return false;
            }
            catch (Exception exception)
            {
                VersionError = exception is TimeoutException
                    ? "WebSocket 版本检测超时，请检查连接并确认服务端已更新。"
                    : "版本检测失败：" + exception.Message;
                Debug.LogWarning("[WeChatPlatform] " + VersionError + " " + exception.Message);
                return false;
            }
            finally { _versionCheckRunning = false; }
        }

        public UniTask ShowVersionRetryAsync()
        {
            var completion = new UniTaskCompletionSource();
            WX.ShowModal(new ShowModalOption
            {
                title = "版本检测",
                content = VersionError ?? "请重新检查客户端版本。",
                showCancel = false,
                confirmText = "重新检测",
                success = _ => completion.TrySetResult(),
                fail = _ => completion.TrySetException(new InvalidOperationException("无法显示版本提示，请重新打开小游戏。"))
            });
            return completion.Task;
        }

        public void ShowVersionError()
        {
            WX.ShowModal(new ShowModalOption
            {
                title = "版本检测",
                content = VersionError ?? "请关闭并重新打开小游戏。",
                showCancel = false,
                confirmText = "知道了"
            });
        }

        // 会话只保存在本次小游戏运行的内存里，不把凭证写入日志或本地明文存储。
        public void ClearLoginSession()
        {
            ++_loginAttempt;
            _openId = null;
            _sessionToken = null;
        }

        public void CacheSession(string openId, string token)
        {
            _openId = openId;
            _sessionToken = token;
        }

        public bool TryGetSession(string openId, out string token)
        {
            token = _sessionToken;
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(_openId) && _openId == openId;
        }

        public void RetryExpiredSession(string serverId)
        {
            if (string.IsNullOrEmpty(_sessionToken)) return;
            _sessionToken = null;
            Debug.Log("[WeChatPlatform] 缓存会话失效，重新获取微信登录凭证");
            RefreshLoginAfterReconnect(serverId);
        }

        public static WeChatPlatformMrg Instance
        {
            get
            {
                if (_instance == null)
                    new GameObject(nameof(WeChatPlatformMrg)).AddComponent<WeChatPlatformMrg>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _updateManager = WX.GetUpdateManager();
            _updateManager.OnUpdateReady(_ =>
            {
                _updateReady = true;
                _updateFailed = false;
                ApplyRequiredUpdate();
            });
            _updateManager.OnUpdateFailed(_ => _updateFailed = true);
            WX.OnHide(OnHide);
            WX.OnShow(OnShow);
            Debug.Log("[WeChatPlatform] 管理器已创建，前后台监听已注册");
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            WX.OffHide(OnHide);
            WX.OffShow(OnShow);
            _instance = null;
        }

        private void OnHide(GeneralCallbackResult result)
        {
            _hidden = true;
            Debug.Log("[WeChatPlatform] OnHide：进入后台");
        }

        private void OnShow(OnShowListenerResult result)
        {
            Debug.Log($"[WeChatPlatform] OnShow：返回前台，已收到后台事件={_hidden}");
            // 首次启动的 OnShow 不触发重连。
            if (!_hidden) return;
            _hidden = false;
            WebNet.ReconnectAfterResume();
        }

        public async UniTask<string> GetLoginCodeAsync()
        {
            var completion = new UniTaskCompletionSource<string>();
            WX.Login(new LoginOption
            {
                success = result =>
                {
                    if (string.IsNullOrWhiteSpace(result.code))
                        completion.TrySetException(new InvalidOperationException("微信登录未返回有效 code。"));
                    else
                        completion.TrySetResult(result.code);
                },
                fail = result => completion.TrySetException(
                    new InvalidOperationException($"微信登录失败：{result.errMsg}"))
            });
            return await completion.Task.Timeout(TimeSpan.FromSeconds(15));
        }

        public void RefreshLoginAfterReconnect(string serverId)
        {
            RefreshLoginAsync(serverId).Forget(exception => Debug.LogException(exception));
        }

        private async UniTask RefreshLoginAsync(string serverId)
        {
            int attempt = ++_loginAttempt;
            int connection = WebNet.ConnectionGeneration;
            if (TryGetSession(_openId, out _))
            {
                Debug.Log("[WeChatPlatform] 连接恢复，复用缓存会话登录");
                WebNet.Login(_openId, serverId);
                return;
            }
            Debug.Log("[WeChatPlatform] 连接恢复，开始刷新微信登录凭证");
            string code = await GetLoginCodeAsync();
            if (attempt != _loginAttempt || connection != WebNet.ConnectionGeneration || !WebNet.IsConnected) return;
            Debug.Log("[WeChatPlatform] 微信凭证获取成功，提交服务器登录");
            WebNet.Login(code, serverId);
        }
    }
}
#endif
