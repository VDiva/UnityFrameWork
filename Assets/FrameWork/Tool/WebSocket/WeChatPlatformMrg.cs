#if WEIXINMINIGAME && !UNITY_EDITOR
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

        // 会话只保存在本次小游戏运行的内存里，不把凭证写入日志或本地明文存储。
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
            if (TryGetSession(_openId, out _))
            {
                Debug.Log("[WeChatPlatform] 连接恢复，复用缓存会话登录");
                WebNet.Login(_openId, serverId);
                return;
            }
            Debug.Log("[WeChatPlatform] 连接恢复，开始刷新微信登录凭证");
            string code = await GetLoginCodeAsync();
            if (attempt != _loginAttempt || !WebNet.IsConnected) return;
            Debug.Log("[WeChatPlatform] 微信凭证获取成功，提交服务器登录");
            WebNet.Login(code, serverId);
        }
    }
}
#endif
