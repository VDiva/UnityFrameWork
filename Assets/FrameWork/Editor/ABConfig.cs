using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FrameWork.Editor;
using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    public class ABConfig : UnityEditor.Editor
    {

        [MenuItem("FrameWork/配置/创建ab包版本文件")]
        public static void CreateConfig()
        {
            CreateConfig(Application.streamingAssetsPath+AssetBundle.GetAbDictoryPath(EditorUserBuildSettings.activeBuildTarget));
        }
        
        // [MenuItem("FrameWork/CreateConfig/CreateAbAndroidConfig")]
        // public static void CreateAbAndroidConfig()
        // {
        //     CreateConfig(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.Android));
        // }
        //
        // [MenuItem("FrameWork/CreateConfig/CreateAbWindowsConfig")]
        // public static void CreateAbWindowsConfig()
        // {
        //     CreateConfig(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.WindowsPlayer));
        // }
        //
        //
        // [MenuItem("FrameWork/CreateConfig/CreateAbIosConfig")]
        // public static void CreateAbIosConfig()
        // {
        //     CreateConfig(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.IPhonePlayer));
        // }
        //
        //
        // [MenuItem("FrameWork/CreateConfig/All")]
        // public static void CreateAll()
        // {
        //     CreateAbWindowsConfig();
        //     CreateAbAndroidConfig();
        // }
        

        private static void CreateConfig(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            
            string info = "";
            foreach (var item in fileInfos)
            {
                if (item.Extension.Equals("."+Config.AbEndName))
                {
                    info += item.Name + " "+item.Length+" "+ GetMd5(item.FullName);
                    info += "|";
                }
            }
            info = info.Substring(0, info.Length - 1);
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            using (StreamWriter sw=new StreamWriter(path+"/"+Config.ConfigName,false))
            {
                sw.Write(info);
            }
            
            
            
            Debug.Log("AB包对比文件生成成功");
            AssetDatabase.Refresh();
        }


        [MenuItem("FrameWork/更新文件AB包名")]
        public static void AssetPackaged()
        {
            if (!Directory.Exists(Config.IsAb?Config.AbAssetPath:Config.ResourcesPath))
            {
                Directory.CreateDirectory(Config.IsAb?Config.AbAssetPath:Config.ResourcesPath);
            }
            
            DirectoryInfo directoryInfo = new DirectoryInfo(Config.IsAb?Config.AbAssetPath:Config.ResourcesPath);
            if (Config.IsAb)
            {
                if (!Directory.Exists(Config.AbClassPath))
                {
                    Directory.CreateDirectory(Config.AbClassPath);
                }
                using (StreamWriter swMode=new StreamWriter(Config.AbClassPath+"/AssetAb.cs",false))
                {
                    swMode.WriteLine("namespace FrameWork\n{");
                    swMode.WriteLine("\tpublic static class AssetAb\n\t{");
                    CheckDirectory(directoryInfo,swMode);
                    swMode.WriteLine("\t}");
                    swMode.WriteLine("}");
                }
            }
            else
            {
                CheckDirectoryAsResources(directoryInfo);
            }
            
            AssetDatabase.Refresh();
        }



        private static void CheckDirectoryAsResources(DirectoryInfo directoryInfo,string abName="")
        {
            var fileInfos = directoryInfo.GetFiles();
            var directoryInfos=directoryInfo.GetDirectories();
            foreach (var item in directoryInfos)
            {
                var str = item.FullName.Split(new string[] { "Resources\\" }, StringSplitOptions.None)[1].Replace("\\","/");
                //_stringBuilder.AppendLine("\t\t"+str+",");
                MyLog.Log(str);
                CheckDirectoryAsResources(item,str);
            }
            
            foreach (var item in fileInfos)
            {
                if (item.Extension!=".meta")
                {
                    CheckFileInfoAsResources(item,abName);
                }
            }
        }

        private static void CheckFileInfoAsResources(FileInfo fileInfo,string abName="other")
        {
            var path = fileInfo.DirectoryName + "\\" + fileInfo.Name;
            var unityPath=path.Split(new string[]{"Assets"},StringSplitOptions.None);
            AssetImporter ai=AssetImporter.GetAtPath("Assets\\"+unityPath[unityPath.Length-1]);
            
            
            ai.assetBundleName = abName;
            ai.assetBundleVariant = Config.AbEndName;
            
            
        }
        

        
        private static void CheckFileInfo(FileInfo fileInfo,StreamWriter sw,string abName="other")
        {
            var path = fileInfo.DirectoryName + "\\" + fileInfo.Name;
            var unityPath=path.Split(new string[]{"Assets"},StringSplitOptions.None);
            AssetImporter ai=AssetImporter.GetAtPath("Assets\\"+unityPath[unityPath.Length-1]);

            
            var id = Tool.GetMd5AsString(fileInfo.Name);
            sw.WriteLine($"\t\t\tpublic static string {fileInfo.Name.Split('.')[0]} = \"{id}\";");
            ai.assetBundleName = id;
            ai.assetBundleVariant = Config.AbEndName;
            
            
        }
        
        private static void CheckDirectory(DirectoryInfo directoryInfo,StreamWriter sw, string abName = "")
        {

            var fileInfos = directoryInfo.GetFiles();
            var directoryInfos = directoryInfo.GetDirectories();
            if (!string.IsNullOrEmpty(abName))sw.WriteLine($"\t\tpublic static class {abName}\n"+"\t\t{");
            foreach (var item in fileInfos)
            {
                if (item.Extension != ".meta")
                {
                    CheckFileInfo(item,sw, abName);
                }
            }
            if (!string.IsNullOrEmpty(abName))sw.WriteLine("\t\t}");
            
            foreach (var item in directoryInfos)
            {
                var str = item.FullName.Split(new string[] { "Asset\\" }, StringSplitOptions.None)[1].Replace("\\", "_");
                //_stringBuilder.AppendLine("\t\t"+str+",");
                CheckDirectory(item,sw, str);
            }
            
            
            
            
        }


        public static string GetMd5(string path)
        {
            using (FileStream fs=new FileStream(path,FileMode.Open))
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] md5Info = md5.ComputeHash(fs);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < md5Info.Length; i++)
                    sb.Append(md5Info[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}