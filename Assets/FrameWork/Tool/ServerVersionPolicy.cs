using System;
using System.Globalization;

namespace FrameWork
{
    /// <summary>独立的服务器兼容版本使用非负整数，客户端低于服务端要求时才更新。</summary>
    public static class ServerVersionPolicy
    {
        public static bool RequiresUpdate(string clientVersion, string serverVersion)
        {
            return Parse(clientVersion, "客户端 serverVersion") < Parse(serverVersion, "服务端 ServerVersion");
        }

        private static long Parse(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long version))
                throw new InvalidOperationException(name + " 必须是非负整数版本号。");
            return version;
        }
    }
}
