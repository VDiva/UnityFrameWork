using System;
using System.Collections.Generic;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 客户端自定义传输协议注册表（例如 "steam://" 等第三方联机网络服务）。
    /// 业务层或 Steam 插件可在启动时注册工厂，无需修改 GameNet 核心代码。
    /// </summary>
    public static class NetTransportRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Func<string, INetTransport>> Factories =
            new Dictionary<string, Func<string, INetTransport>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>注册协议前缀对应的传输层工厂，例如 scheme = "steam://"。</summary>
        public static void RegisterScheme(string scheme, Func<string, INetTransport> factory)
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("协议前缀不能为空。", nameof(scheme));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            string normalized = scheme.Trim();
            if (!normalized.EndsWith("://", StringComparison.Ordinal))
                normalized += "://";

            lock (Gate)
            {
                Factories[normalized] = factory;
            }
        }

        public static void UnregisterScheme(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme)) return;
            string normalized = scheme.Trim();
            if (!normalized.EndsWith("://", StringComparison.Ordinal))
                normalized += "://";

            lock (Gate)
            {
                Factories.Remove(normalized);
            }
        }

        public static bool TryCreate(string address, out INetTransport transport)
        {
            transport = null;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            int idx = address.IndexOf("://", StringComparison.Ordinal);
            if (idx <= 0)
                return false;

            string scheme = address.Substring(0, idx + 3);
            Func<string, INetTransport> factory;
            lock (Gate)
            {
                if (!Factories.TryGetValue(scheme, out factory))
                    return false;
            }

            transport = factory?.Invoke(address);
            return transport != null;
        }
    }
}
