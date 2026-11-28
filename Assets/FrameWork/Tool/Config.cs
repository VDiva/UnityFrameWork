using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FrameWork
{
    public static class Config
    {
        public const string AssetPath = "Assets/FrameWork/Asset/Base/ConfigData.asset";
        private static ConfigData _loaded;
        private static ConfigData _configData => _loaded != null ? _loaded :
            throw new InvalidOperationException("请在业务启动前 await Config.InitializeAsync() 加载热更新配置。");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _loaded = null;

        public static async UniTask InitializeAsync()
        {
            if (_loaded != null) return;
            var config = await ABMrg.LoadAsync<ConfigData>(AssetPath);
            if (config == null)
                throw new InvalidOperationException("业务配置加载结果为空：" + AssetPath);
            _loaded = config;
        }

        public static string ServerIp => _configData.serverIp;
        public static string DebugIp => _configData.debugServerIp;
        public static NativeTransportMode NativeTransport => _configData.nativeTransportMode;
        public static int TcpPort => _configData.tcpPort > 0 ? _configData.tcpPort : 5101;
        public static int KcpPort => _configData.kcpPort > 0 ? _configData.kcpPort : 5102;
        public static string TcpServerIp => ResolveTransportAddress(_configData.tcpServerIp, _configData.serverIp, "tcp", TcpPort);
        public static string DebugTcpIp => ResolveTransportAddress(_configData.debugTcpServerIp, _configData.debugServerIp, "tcp", TcpPort);
        public static string KcpServerIp => ResolveTransportAddress(_configData.kcpServerIp, _configData.serverIp, "kcp", KcpPort);
        public static string DebugKcpIp => ResolveTransportAddress(_configData.debugKcpServerIp, _configData.debugServerIp, "kcp", KcpPort);
        public static string SteamServerAddress => _configData.steamTargetAddress;
        public static string ClientVersion => _configData.versions;
        public static string ServerVersion => _configData.serverVersion;
        public static long ConvertDateTimep(DateTime time) => new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeSeconds();

        /// <summary>
        /// 若显式配置了协议地址则直接使用；否则自动从 WebSocket 地址提取主机并拼接目标协议与端口。
        /// </summary>
        public static string ResolveTransportAddress(string explicitAddress, string fallbackWebSocketUrl, string scheme, int port)
        {
            if (!string.IsNullOrWhiteSpace(explicitAddress))
            {
                string trimmed = explicitAddress.Trim();
                return trimmed.Contains("://") ? trimmed : $"{scheme}://{trimmed}";
            }

            if (string.IsNullOrWhiteSpace(fallbackWebSocketUrl))
                return $"{scheme}://127.0.0.1:{port}";

            string source = fallbackWebSocketUrl.Trim();
            if (!source.Contains("://"))
                source = "ws://" + source;

            if (Uri.TryCreate(source, UriKind.Absolute, out Uri uri) && !string.IsNullOrWhiteSpace(uri.Host))
                return $"{scheme}://{uri.Host}:{port}";

            return $"{scheme}://127.0.0.1:{port}";
        }
    }
}
