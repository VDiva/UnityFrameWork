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
    // [UnityEditor.AssetImporters.ScriptedImporter(1,"")]
    // public class ABInputTool:UnityEditor.AssetImporters.ScriptedImporter
    // {
    //     public static List<string> paths = new List<string>();
    //     
    //     //public static ConfigData ConfigData;
    //     public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    //     {
    //         if (ctx.assetPath.IndexOf(ABEditor.abAssetPath, StringComparison.Ordinal)==-1) return;
    //         Debug.Log("导入对象");
    //         var path = ctx.assetPath;
    //         var fileName = Path.GetFileNameWithoutExtension(path);
    //         if (fileName.StartsWith("~$")) return;
    //         paths.Add(path); //添加到待转换列表里 等待资源加载完成后转换
    //     }
    // }
    //
    //
    // public class ABInputAssetPostprocessor : AssetPostprocessor
    // {
    //     private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
    //         string[] movedAssets,
    //         string[] movedFromAssetPaths)
    //     {
    //
    //         // if (ABInputTool.paths == null
    //         //     || ABInputTool.paths.Count == 0) return;
    //
    //         if (EditorUtility.DisplayDialog("信息", "检测到资源发生变换是否更新资源包", "是", "否"))
    //         {
    //             UpDatePack();
    //         }
    //
    //         ABInputTool.paths.Clear();
    //     }
    //
    //
    //
    //     public static void UpDatePack()
    //     {
    //         for (int i = 0; i < ABInputTool.paths.Count; i++)
    //         {
    //             var groupName= ABInputTool.paths[i];
    //             var n = Path.GetFileNameWithoutExtension(groupName);
    //             groupName=groupName.Split("Assets/FrameWork/Asset")[1];
    //             groupName=groupName.Split("n")[0];
    //             groupName = groupName.Replace("/", "");
    //             var group = ABEditor.setting.FindGroup(groupName);
    //             if (group == null)
    //             {
    //                 group=ABEditor.setting.CreateGroup(groupName,false, false, false, new List<AddressableAssetGroupSchema> { ABEditor.setting.DefaultGroup.Schemas[0] }, typeof(SchemaType));
    //             }
    //             var guid = AssetDatabase.AssetPathToGUID(ABEditor.abAssetPath+"/"+groupName+"/"+n);
    //             Debug.Log(guid);
    //             var addressableAsset=ABEditor.setting.CreateOrMoveEntry(guid, group);
    //             addressableAsset.address = n;
    //         }
    //     }
    // }


    public class ABEditor : UnityEditor.Editor
    {
        
        public static string abAssetPath = "Assets/FrameWork/Asset";
        public static readonly AddressableAssetSettings setting = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
        static ABEditor()
        {
            AddPack();
        }
        
        [MenuItem("FrameWork/Addressables/InstallPack")]
        public static void AddPack()
        {
            Client.Add("com.unity.addressables.cn");
        }

        [MenuItem("FrameWork/UpdateScript")]
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

        public static string GetMd5AsString(string key)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] md5Info = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < md5Info.Length; i++)
                sb.Append(md5Info[i].ToString("x2"));
            return sb.ToString();
        }
    }
}