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
            CreateConfig(Application.streamingAssetsPath+"/Android");
        }
        
        [MenuItem("FrameWork/CreateConfig/CreateAbWindowsConfig")]
        public static void CreateAbWindowsConfig()
        {
            CreateConfig(Application.streamingAssetsPath+"/StandaloneWindows");
        }

        
        [MenuItem("FrameWork/CreateConfig/CreateAbIosConfig")]
        public static void CreateAbIosConfig()
        {
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
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            using (StreamWriter sw=new StreamWriter(path+"/"+GlobalVariables.Configure.ConfigName,false))
            {
                sw.Write(info);
            }
            
            
            
            MyLog.Log("AB包对比文件生成成功");
            AssetDatabase.Refresh();
        }


        [MenuItem("FrameWork/AssetPackaged")]
        public static void AssetPackaged()
        {
            if (!Directory.Exists(GlobalVariables.Configure.AbAssetPath))
            {
                Directory.CreateDirectory(GlobalVariables.Configure.AbAssetPath);
            }
            
            DirectoryInfo directoryInfo = new DirectoryInfo(GlobalVariables.Configure.AbAssetPath);
            CheckDirectory(directoryInfo);
            
           
            AssetDatabase.Refresh();
        }


        private static void CheckFileInfo(FileInfo fileInfo,string abName="other")
        {
            var path = fileInfo.DirectoryName + "\\" + fileInfo.Name;
            var unityPath=path.Split(new string[]{"Assets"},StringSplitOptions.None);
            AssetImporter ai=AssetImporter.GetAtPath("Assets\\"+unityPath[unityPath.Length-1]);
            
            
            ai.assetBundleName = abName;
            ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
            
            
        }

        private static void CheckDirectory(DirectoryInfo directoryInfo,string abName="")
        {
            var fileInfos = directoryInfo.GetFiles();
            var directoryInfos=directoryInfo.GetDirectories();
            foreach (var item in directoryInfos)
            {
                var str = item.FullName.Split(new string[] { "Asset\\" }, StringSplitOptions.None)[1].Replace("\\","_");
                //_stringBuilder.AppendLine("\t\t"+str+",");
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