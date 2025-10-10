using System;
using UnityEngine;

namespace FrameWork
{
    public static class Config
    {
        private static ConfigData _configData;
        static Config()
        {
            _configData=Resources.Load<ConfigData>("ConfigData");
        }
        
        /// <summary>
        /// 服务器ip
        /// </summary>
        public static string ServerIp=> _configData.serverIp;
        
        /// <summary>
        /// 服务器端口
        /// </summary>
        public static int ServerPort=> _configData.serverPort;
        /// <summary>
        /// 是否是ab包方式 不是则是resources方式
        /// </summary>
        public static bool IsAb => _configData.isAb;
        
        /// <summary>
        /// ab包版本文件名
        /// </summary>
        public static string ConfigName => _configData.configName;

        /// <summary>
        /// ase加密key
        /// </summary>
        public static string Key => _configData.key;
        
        /// <summary>
        /// 热更新dll
        /// </summary>
        public static string[] Dlls => _configData.dlls;


        public static string DataPath = Application.dataPath.Replace("Assets", "");
        
        /// <summary>
        /// 热更新dll目录
        /// </summary>
        public static string DllPath = DataPath+"\\HybridCLRData\\HotUpdateDlls\\";
        
        /// <summary>
        /// 热更新dll复制目录
        /// </summary>
        public static string DllCopyPath = DataPath+"\\Assets\\FrameWork\\Asset\\Dll\\";
        
        /// <summary>
        /// 热更新下载地址
        /// </summary>
        public static string DownLoadUrl => _configData.downLoadUrl;
        
        public static string GetAbPath()
        {
            string path = "";
            RuntimePlatform platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    path ="/StandaloneWindows64/";
                    break;
                case RuntimePlatform.Android:
                    path = "/Android/";
                    break;
                case RuntimePlatform.IPhonePlayer:
                    path = "/Ios/";
                    break;
                case RuntimePlatform.WebGLPlayer:
                    path = "/WebGl/";
                    break;
            }

            path += _configData.versions + "/";
            return path;
        }
        
        
        
        
        
        public static long ConvertDateTimep(DateTime time)
        {
            return ((time.ToUniversalTime().Ticks - 621355968000000000) / 10000000);
            //等价于：
            //return ((time.ToUniversalTime().Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks) / 10000000) * 1000;
        }
    }
}