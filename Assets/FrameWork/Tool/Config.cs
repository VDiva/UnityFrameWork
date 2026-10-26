using System;
using UnityEngine;

namespace FrameWork
{
    public static class Config
    {
        private static readonly ConfigData _configData = Resources.Load<ConfigData>("ConfigData");
        public static string ServerIp => _configData.serverIp;
        public static string DebugIp => _configData.debugServerIp;
        public static string ClientVersion => _configData.versions;
        public static string ServerVersion => _configData.serverVersion;
        public static long ConvertDateTimep(DateTime time) => new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeSeconds();
    }
}
