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
        public static string ClientVersion => _configData.versions;
        public static string ServerVersion => _configData.serverVersion;
        public static long ConvertDateTimep(DateTime time) => new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeSeconds();
    }
}
