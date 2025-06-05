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
        /// 是否是ab包方式 不是则是resources方式
        /// </summary>
        public static bool IsAb => _configData.isAb;

        /// <summary>
        /// 生成ab包包名的类的位置
        /// </summary>
        public static string AbClassPath => _configData.abClassPath;

        /// <summary>
        /// ab包生成的结尾名
        /// </summary>
        public static string AbEndName => _configData.abEndName;

        /// <summary>
        /// ab包路径
        /// </summary>
        public static string AbAssetPath => _configData.abAssetPath;

        /// <summary>
        /// ab包版本文件名
        /// </summary>
        public static string ConfigName => _configData.configName;

        /// <summary>
        /// ase加密key
        /// </summary>
        public static string Key => _configData.key;


        public static string XlsxPath => _configData.xlsxPath;

        public static string XlsxOutPath => _configData.xlsxOutPath;

        public static string XlsxOutResourcesPath => _configData.xlsxOutResourcesPath;

        public static string XlsxOutScriptPath => _configData.xlsxOutScriptPath;


        /// <summary>
        /// 配置表生成引用
        /// </summary>
        public static string[] XlsxSpawnUse => _configData.xlsxSpawnUse;

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

        /// <summary>
        /// 预制体生成代码引用
        /// </summary>
        public static string[] SpawnScriptUse => _configData.spawnScriptUse;

        /// <summary>
        /// 预制体生成代码路径
        /// </summary>
        public static string SpawnScriptPath => _configData.spawnScriptPath;
        //public static string spawnXlsxScriptPath = "Assets/FrameWork/Xlsx";
        
        /// <summary>
        /// resources 获取预制体的路径
        /// </summary>
        public static string ResourcesPath => _configData.resourcesPath;


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
        
        public static string GetAbPath(RuntimePlatform platform)
        {
            string path = "";
            //RuntimePlatform platform = Application.platform;
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
                    path = Application.persistentDataPath + "/WebGl/";
                    break;
            }
            path += _configData.versions + "/";
            return path;
        }
        
        public static string GetAbDictoryPath()
        {
            string path = "";
            RuntimePlatform platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    path = "/StandaloneWindows64/";
                    break;
                case RuntimePlatform.Android:
                    path =  "/Android/";
                    break;
                case RuntimePlatform.IPhonePlayer:
                    path ="/Ios/";
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