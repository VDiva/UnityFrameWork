using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    public class ABConfig : UnityEditor.Editor 
    {
        [MenuItem("FrameWork/CreateConfig/CreateAbAndroidConfig")]
        public static void CreateAbAndroidConfig()
        {
            // string path = Application.dataPath+"/FrameWork/Config";
            // if (Application.platform == RuntimePlatform.Android) path = GlobalVariables.Configure.AbAndroidPath;
            // if (Application.platform == RuntimePlatform.WindowsPlayer) path =GlobalVariables.Configure.AbWindowsPath;
            // if (Application.platform == RuntimePlatform.IPhonePlayer) path = GlobalVariables.Configure.AbIosPath;
            // if (Application.platform == RuntimePlatform.WindowsEditor) path = GlobalVariables.Configure.AbWindowsPath;
            CreateConfig(Application.streamingAssetsPath+"/Android");
            
        }
        
        [MenuItem("FrameWork/CreateConfig/CreateAbWindowsConfig")]
        public static void CreateAbWindowsConfig()
        {
            // string path = Application.dataPath+"/FrameWork/Config";
            // if (Application.platform == RuntimePlatform.Android) path = GlobalVariables.Configure.AbAndroidPath;
            // if (Application.platform == RuntimePlatform.WindowsPlayer) path =GlobalVariables.Configure.AbWindowsPath;
            // if (Application.platform == RuntimePlatform.IPhonePlayer) path = GlobalVariables.Configure.AbIosPath;
            // if (Application.platform == RuntimePlatform.WindowsEditor) path = GlobalVariables.Configure.AbWindowsPath;
            CreateConfig(Application.streamingAssetsPath+"/StandaloneWindows");
            
        }

        
        [MenuItem("FrameWork/CreateConfig/CreateAbIosConfig")]
        public static void CreateAbIosConfig()
        {
            // string path = Application.dataPath+"/FrameWork/Config";
            // if (Application.platform == RuntimePlatform.Android) path = GlobalVariables.Configure.AbAndroidPath;
            // if (Application.platform == RuntimePlatform.WindowsPlayer) path =GlobalVariables.Configure.AbWindowsPath;
            // if (Application.platform == RuntimePlatform.IPhonePlayer) path = GlobalVariables.Configure.AbIosPath;
            // if (Application.platform == RuntimePlatform.WindowsEditor) path = GlobalVariables.Configure.AbWindowsPath;
            CreateConfig(Application.streamingAssetsPath+"/StandaloneWindows");
            
        }
        

        private static void CreateConfig(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            
            string info = "";
            foreach (var item in fileInfos)
            {
                if (item.Extension.Equals("."+GlobalVariables.Configure.AbEndName))
                {
                    info += item.Name + " "+item.Length+" "+ Tool.GetMd5(item.FullName);
                    info += "|";
                }
            }
            info = info.Substring(0, info.Length - 1);
            
            // if (!Directory.Exists(GlobalVariables.Configure.ConfigPath))
            // {
            //     Directory.CreateDirectory(GlobalVariables.Configure.ConfigPath);
            // }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            using (StreamWriter sw=new StreamWriter(path+"/"+GlobalVariables.Configure.ConfigName,false))
            {
                sw.Write(info);
            }
            
            
            
            Debug.Log("AB包对比文件生成成功");
            AssetDatabase.Refresh();
        }


        private static StringBuilder _stringBuilder = new StringBuilder();
        private static string _path = "Assets/FrameWork/Ui/Type/";
        [MenuItem("FrameWork/AssetPackaged")]
        public static void AssetPackaged()
        {
            if (!Directory.Exists(GlobalVariables.Configure.AbAssetPath))
            {
                Directory.CreateDirectory(GlobalVariables.Configure.AbAssetPath);
            }
            
            _stringBuilder.Clear();
            _stringBuilder.AppendLine("using System.Collections.Generic;");
            _stringBuilder.AppendLine("using UnityEngine;");
            _stringBuilder.AppendLine("using FrameWork;");
            _stringBuilder.AppendLine("namespace FrameWork");
            _stringBuilder.AppendLine("{");
            _stringBuilder.AppendLine("\tpublic enum "+"AssetType");
            _stringBuilder.AppendLine("\t{");
            
            DirectoryInfo directoryInfo = new DirectoryInfo(GlobalVariables.Configure.AbAssetPath);
            CheckDirectory(directoryInfo);
            
            _stringBuilder.AppendLine("\t}");
            _stringBuilder.AppendLine("}");

            if (!Directory.Exists(_path))
            {
                Directory.CreateDirectory(_path);
            }
            
            File.WriteAllText(_path+"AssetType.cs",_stringBuilder.ToString());
            AssetDatabase.Refresh();
        }


        private static void CheckFileInfo(FileInfo fileInfo,string abName="other")
        {
            var path = fileInfo.DirectoryName + "\\" + fileInfo.Name;
            var unityPath=path.Split(new string[]{"Assets"},StringSplitOptions.None);
            AssetImporter ai=AssetImporter.GetAtPath("Assets\\"+unityPath[unityPath.Length-1]);
            //Debug.Log(path);
            
            ai.assetBundleName = abName;
            ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            
            // if (fileInfo.Extension.Equals(".prefab"))
            // {
            //     ai.assetBundleName = GlobalVariables.Configure.AbModePrefabName;
            //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            // }
            // else if (fileInfo.Extension.Equals(".mat"))
            // {
            //     ai.assetBundleName = GlobalVariables.Configure.AbMaterialName;
            //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            // }else if (fileInfo.Extension.Equals(".unity"))
            // {
            //     ai.assetBundleName = GlobalVariables.Configure.AbScreenName;
            //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            // }
            
            
            // //FrameWork.Type.BuildTarget buildTarget = FrameWork.Type.BuildTarget.Windows;
            // RuntimePlatform platform = Application.platform;
            //         
            // switch (platform)
            // {
            //     case RuntimePlatform.WindowsEditor:
            //         //buildTarget = FrameWork.Type.BuildTarget.Windows;
            //         AssetBundle.CreatPCAssetBundleAsWindows();
            //         break;
            //     case RuntimePlatform.WindowsPlayer:
            //         //buildTarget = FrameWork.Type.BuildTarget.Windows;
            //         AssetBundle.CreatPCAssetBundleAsWindows();
            //         break;
            //     case RuntimePlatform.Android:
            //         //buildTarget = FrameWork.Type.BuildTarget.Android;
            //         AssetBundle.a();
            //         break;
            //     case RuntimePlatform.IPhonePlayer:
            //         //buildTarget = FrameWork.Type.BuildTarget.Windows;
            //         break;
            // }
            
        }

        private static void CheckDirectory(DirectoryInfo directoryInfo,string abName="")
        {
            var fileInfos = directoryInfo.GetFiles();
            var directoryInfos=directoryInfo.GetDirectories();
            foreach (var item in directoryInfos)
            {
                var str = item.FullName.Split(new string[] { "Asset\\" }, StringSplitOptions.None)[1].Replace("\\","_");
                _stringBuilder.AppendLine("\t\t"+str+",");
                CheckDirectory(item,str);
            }
            
            foreach (var item in fileInfos)
            {
                if (item.Extension!=".meta")
                {
                    CheckFileInfo(item,abName);
                }
            }
        }
    }
}