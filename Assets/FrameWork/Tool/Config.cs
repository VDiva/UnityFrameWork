using System;
using UnityEngine;

namespace FrameWork
{
    public static class Config
    {
        public static string abEndName = "info";
        public static string abAssetPath = "Assets/FrameWork/Asset";
        public static string configName = "ABConfig.txt";
        public static string key = "kljsdkkdlo4454GG00155sajuklmbkdl";
        
        
        public static string[] dlls = new String[] { "HotUpdate.dll" };
        public static string dataPath = Application.dataPath.Replace("Assets", "");
        public static string dllPath = dataPath+"\\HybridCLRData\\HotUpdateDlls\\";
        public static string dllCopyPath = dataPath+"\\Assets\\FrameWork\\Asset\\Dll\\";
        
        public static string DownLoadUrl="http://127.0.0.1:3000";
        
        public static string spawnScriptPath = "Assets/FrameWork/Scripts/PrefabScript";
        
        
        
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
            }

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