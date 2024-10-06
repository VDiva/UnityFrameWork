using System;
using UnityEngine;

namespace FrameWork
{
    public static class Config
    {

        /// <summary>
        /// 是否是ab包方式 不是则是resources方式
        /// </summary>
        public static bool IsAb=true;

        /// <summary>
        /// 生成ab包包名的类的位置
        /// </summary>
        public static string abClassPath = "Assets/FrameWork/Scripts/AssetAb";
        
        /// <summary>
        /// ab包生成的结尾名
        /// </summary>
        public static string abEndName = "info";
        
        /// <summary>
        /// ab包路径
        /// </summary>
        public static string abAssetPath = "Assets/FrameWork/Asset";
        
        /// <summary>
        /// ab包版本文件名
        /// </summary>
        public static string configName = "ABConfig.txt";
        
        /// <summary>
        /// ase加密key
        /// </summary>
        public static string key = "kljsdkkdlo4454GG00155sajuklmbkdl";

        /// <summary>
        /// 配置表生成引用
        /// </summary>
        public static string[] XlsxSpawnUse = new string[] {"System.Collections.Generic","UnityEngine","Xlsx"};
        
        /// <summary>
        /// 热更新dll
        /// </summary>
        public static string[] dlls = new String[] { "HotUpdate.dll"};
        
        
        public static string dataPath = Application.dataPath.Replace("Assets", "");
        
        /// <summary>
        /// 热更新dll目录
        /// </summary>
        public static string dllPath = dataPath+"\\HybridCLRData\\HotUpdateDlls\\";
        
        /// <summary>
        /// 热更新dll复制目录
        /// </summary>
        public static string dllCopyPath = dataPath+"\\Assets\\FrameWork\\Asset\\Dll\\";
        
        /// <summary>
        /// 热更新下载地址
        /// </summary>
        public static string DownLoadUrl="http://127.0.0.1:3000";
        
        /// <summary>
        /// 预制体生成代码引用
        /// </summary>
        public static string[] spawnScriptUse = new string[] {"System.Collections.Generic","UnityEngine","Xlsx","UnityEngine.UI","UnityEngine.Video","Spine.Unity","TMPro","Pathfinding","ScriptCode.Move","ScriptCode.Tool"};
        
        /// <summary>
        /// 预制体生成代码路径
        /// </summary>
        public static string spawnScriptPath = "Assets/FrameWork/Scripts/PrefabScript";
        //public static string spawnXlsxScriptPath = "Assets/FrameWork/Xlsx";
        
        /// <summary>
        /// resources 获取预制体的路径
        /// </summary>
        public static string ResourcesPath="Assets/Resources/Asset";

        
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