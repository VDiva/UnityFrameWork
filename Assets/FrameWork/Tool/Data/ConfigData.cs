﻿using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FrameWork
{
    [CreateAssetMenu(fileName = "ConfigData", menuName = "FrameWork/CreateConfigData")]
    public class ConfigData: ScriptableObject
    {
        /// <summary>
        /// 是否是ab包方式 不是则是resources方式
        /// </summary>
        public bool isAb=true;

        /// <summary>
        /// 生成ab包包名的类的位置
        /// </summary>
        public string abClassPath = "Assets/FrameWork/Scripts/AssetAb";
        
        /// <summary>
        /// ab包生成的结尾名
        /// </summary>
        public string abEndName = "info";
        
        /// <summary>
        /// ab包路径
        /// </summary>
        public string abAssetPath = "Assets/FrameWork/Asset";
        
        /// <summary>
        /// ab包版本文件名
        /// </summary>
        public string configName = "ABConfig.txt";
        
        /// <summary>
        /// ase加密key
        /// </summary>
        public string key = "kljsdkkdlo4454GG00155sajuklmbkdl";

        /// <summary>
        /// 配置表生成引用
        /// </summary>
        public string[] xlsxSpawnUse = new string[] {"System.Collections.Generic","UnityEngine","Xlsx","FrameWork"};
        
        /// <summary>
        /// 热更新dll
        /// </summary>
        public string[] dlls = new String[] { "HotUpdate.dll"};
        
        
        // public string dataPath = Application.dataPath.Replace("Assets", "");
        //
        //  /// <summary>
        //  /// 热更新dll目录
        //  /// </summary>
        //  public string dllPath = dataPath+"\\HybridCLRData\\HotUpdateDlls\\";
        //
        //  /// <summary>
        //  /// 热更新dll复制目录
        //  /// </summary>
        //  public string dllCopyPath = dataPath+"\\Assets\\FrameWork\\Asset\\Dll\\";
        
        /// <summary>
        /// 热更新下载地址
        /// </summary>
        public string downLoadUrl="http://127.0.0.1:3000";
        
        /// <summary>
        /// 预制体生成代码引用
        /// </summary>
        public string[] spawnScriptUse = new string[] {"System.Collections.Generic","UnityEngine","UnityEngine.UI"};
        
        /// <summary>
        /// 预制体生成代码路径
        /// </summary>
        public string spawnScriptPath = "Assets/FrameWork/Scripts/PrefabScript";
        //public static string spawnXlsxScriptPath = "Assets/FrameWork/Xlsx";
        
        /// <summary>
        /// resources 获取预制体的路径
        /// </summary>
        public string resourcesPath="Assets/Resources/Asset";
    }
}