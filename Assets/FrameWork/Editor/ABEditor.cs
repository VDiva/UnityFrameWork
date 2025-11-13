using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FrameWork.Editor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

#if ADDRESSABLESCN_INSTALLED
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif
namespace FrameWork
{
    [InitializeOnLoad]
    public class ABEditor : UnityEditor.Editor
    {
        public static string abAssetPath = "Assets/FrameWork/Asset";
        
        static ABEditor()
        {
            CheckPackagesAndGenerateDefines();
        }
        
        
        // 需要检查的包及其对应的宏定义
        private static readonly Dictionary<string, string> PackageMacros = new Dictionary<string, string>
        {
            {"com.unity.addressables.cn", "ADDRESSABLESCN_INSTALLED"}
        };
        
        
        [MenuItem("FrameWork/Addressables/更新宏定义")]
        static void CheckPackagesAndGenerateDefines()
        {
            ListRequest listRequest = Client.List();
        
            // 等待包列表请求完成
            while (!listRequest.IsCompleted)
            {
                if (listRequest.Status == StatusCode.Failure || listRequest.Error != null)
                {
                    Debug.LogError($"获取包列表失败: {listRequest.Error.message}");
                    return;
                }
            }

            // 收集已安装包对应的宏定义
            HashSet<string> defines = new HashSet<string>();
            foreach (var package in listRequest.Result)
            {
                if (PackageMacros.TryGetValue(package.name, out string macro))
                {
                    defines.Add(macro);
                    Debug.Log($"检测到已安装包: {package.name}，启用宏: {macro}");
                }
            }

            // 更新宏定义
            UpdateScriptingDefines(defines);
        }
        
        
        static void UpdateScriptingDefines(HashSet<string> newDefines)
        {
            // 获取当前的宏定义
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            HashSet<string> currentDefines = new HashSet<string>(definesString.Split(';'));

            // 仅保留我们管理的宏定义
            foreach (var macro in PackageMacros.Values)
            {
                if (!newDefines.Contains(macro))
                {
                    currentDefines.Remove(macro);
                }
            }

            // 添加新的宏定义
            foreach (var define in newDefines)
            {
                currentDefines.Add(define);
            }

            // 更新宏定义
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                targetGroup, 
                string.Join(";", currentDefines)
            );
        }
        
        
        [MenuItem("FrameWork/Addressables/安装包体")]
        public static void AddPack()
        {
            var addRequest=Client.Add("com.unity.addressables.cn");
            while (!addRequest.IsCompleted)
            {
                
            }
            
            CheckPackagesAndGenerateDefines();
            
        }

        [MenuItem("FrameWork/刷新代码")]
        public static void UpdateScript()
        {
            CompilationPipeline.RequestScriptCompilation();
        }

        
        [MenuItem("FrameWork/更新资源")]
        public static void AssetPackaged()
        {
#if ADDRESSABLESCN_INSTALLED
            if (!Directory.Exists(abAssetPath))
            {
                Directory.CreateDirectory(abAssetPath);
            }
            DirectoryInfo directoryInfo = new DirectoryInfo(abAssetPath);
            CheckDirectory(directoryInfo);
            AssetDatabase.Refresh();
#endif
        }
        
#if ADDRESSABLESCN_INSTALLED
        
        
        public static readonly AddressableAssetSettings setting = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
        
        
        
        private static void CheckFileInfo(FileInfo fileInfo,string dicPath,string dicName,string abName="other")
        {
            //var path = fileInfo.DirectoryName + "\\" + fileInfo.Name;
            if (string.IsNullOrEmpty(dicPath))return;
            var group = setting.FindGroup(dicPath);
            if (group == null)
            {
                group=setting.CreateGroup(dicPath,false, false, false, new List<AddressableAssetGroupSchema> { setting.DefaultGroup.Schemas[0] }, typeof(SchemaType));
            }
            group.AddSchema<BundledAssetGroupSchema>();
            var path = abAssetPath + "/" + dicName + "/" + fileInfo.Name;
            
            var guid = AssetDatabase.AssetPathToGUID(path);
            Debug.Log(path);
            if (!string.IsNullOrEmpty(guid))
            {
                var addressableAsset=setting.CreateOrMoveEntry(guid, group);
                if (addressableAsset!=null)
                {
                    addressableAsset.address = fileInfo.Name.Split(".")[0];
                }
            }
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
                    CheckFileInfo(item,directoryInfo.FullName.Split(@"\Asset\")[1].Replace("\\","_"),directoryInfo.FullName.Split(@"\Asset\")[1],abName);
                }
            }
            
        }
#endif
        
    }
}