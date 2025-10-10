using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FrameWork.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace FrameWork
{
    public class ABEditor : UnityEditor.Editor
    {
        public static string abAssetPath = "Assets/FrameWork/Asset";
        public static readonly AddressableAssetSettings setting = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
        static ABEditor()
        {
            AddPack();
        }
        
        [MenuItem("FrameWork/Addressables/安装包体")]
        public static void AddPack()
        {
            Client.Add("com.unity.addressables.cn");
        }

        [MenuItem("FrameWork/刷新代码")]
        public static void UpdateScript()
        {
            CompilationPipeline.RequestScriptCompilation();
        }
        
        
        [MenuItem("FrameWork/配置/创建ab包版本文件")]
        public static void CreateConfig()
        {
            //CreateConfig(Application.streamingAssetsPath+AssetBundle.GetAbDictoryPath(EditorUserBuildSettings.activeBuildTarget));
        }
        
        [MenuItem("FrameWork/更新资源")]
        public static void AssetPackaged()
        {
            if (!Directory.Exists(abAssetPath))
            {
                Directory.CreateDirectory(abAssetPath);
            }
            DirectoryInfo directoryInfo = new DirectoryInfo(abAssetPath);
            CheckDirectory(directoryInfo);
            AssetDatabase.Refresh();
        }
        
        private static void CheckFileInfo(FileInfo fileInfo,string abPath,string abName="other")
        {
            //var path = fileInfo.DirectoryName + "\\" + fileInfo.Name;
            var group = setting.FindGroup(abPath);
            if (group == null)
            {
                group=setting.CreateGroup(abPath,false, false, false, new List<AddressableAssetGroupSchema> { setting.DefaultGroup.Schemas[0] }, typeof(SchemaType));
            }
            var guid = AssetDatabase.AssetPathToGUID(abAssetPath+"/"+abPath+"/"+fileInfo.Name);
            Debug.Log(guid);
            var addressableAsset=setting.CreateOrMoveEntry(guid, group);
            addressableAsset.address = fileInfo.Name.Split(".")[0];
        }
        
        private static void CheckDirectory(DirectoryInfo directoryInfo, string abName = "")
        {
            var fileInfos = directoryInfo.GetFiles();
            var directoryInfos = directoryInfo.GetDirectories();
            foreach (var item in directoryInfos)
            {
                var str = item.FullName.Split(new string[] { "Asset\\" }, StringSplitOptions.None)[1].Replace("\\", "_");
                //_stringBuilder.AppendLine("\t\t"+str+",");
                CheckDirectory(item, str);
            }
            
            foreach (var item in fileInfos)
            {
                if (item.Extension!=".meta")
                {
                    CheckFileInfo(item,directoryInfo.Name,abName);
                }
            }
            
        }
    }
}