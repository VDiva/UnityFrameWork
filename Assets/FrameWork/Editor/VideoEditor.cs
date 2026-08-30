using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork.Editor
{
    public class VideoEditor : UnityEditor.Editor
    {


        private static string _videoPath = $"/Assets/Video/Nor";
        private static string _outVideoPath = $"/Assets/StreamingAssets/Video/Nor";

        
        private static string _videoPath4k = "/Assets/Video/High";
        private static string _outVideoPath4k = $"/Assets/StreamingAssets/Video/High";

        
        [MenuItem("FrameWork/视频处理/加密视频文件并复制到目录下普通目录下")]
        public static void CopyVideo()
        {
            
            var path=Application.dataPath.Replace("Assets", "");
            var filePath= path + _videoPath;
            var outPath = path + _outVideoPath;

            // if (!Directory.Exists(outPath))
            // {
            //     Directory.CreateDirectory(outPath);
            // }
            //
            // if (!Directory.Exists(filePath))
            // {
            //     Directory.CreateDirectory(filePath);
            // }
            
            
            
            var dir = new DirectoryInfo(filePath);
            CheckDir(dir);
            
            void CheckDir(DirectoryInfo directoryInfo)
            {
                
                var dirs= directoryInfo.GetDirectories();
                for (int i = 0; i < dirs.Length; i++)
                {
                    CheckDir(dirs[i]);
                }
                
                
                var files=directoryInfo.GetFiles("*.mp4");
                for (int i = 0; i < files.Length; i++)
                {
                    CheckFile(files[i]);
                }
                
            }

            void CheckFile(FileInfo fileInfo,bool isVideo=true)
            {
                //var dirName = Path.GetDirectoryName(fileInfo.FullName);
                var outName =Path.GetFileNameWithoutExtension(fileInfo.Name);
                var fileDir=Path.GetDirectoryName(fileInfo.FullName);

                if (!string.IsNullOrEmpty(fileDir))
                {
                    var muLu = fileDir.Split("Nor\\")[1];

                    var outDir = outPath + "/" + muLu + "/";
                    var outP=outDir+ outName+".Png";

                    if (!isVideo)
                    {
                        outP = outDir + outName + ".Jpg";
                    }
                    
                    AddHeaderObfuscation(fileInfo.FullName,outP);
                }
                
            }
            
            AssetDatabase.Refresh();
        }
        
        
        
        [MenuItem("FrameWork/视频处理/加密视频文件并复制到目录下4k视频目录下")]
        public static void CopyVideo4k()
        {
            
            var path=Application.dataPath.Replace("Assets", "");
            var filePath= path + _videoPath4k;
            var outPath = path + _outVideoPath4k;

            // if (!Directory.Exists(outPath))
            // {
            //     Directory.CreateDirectory(outPath);
            // }
            //
            // if (!Directory.Exists(filePath))
            // {
            //     Directory.CreateDirectory(filePath);
            // }
            
            
            
            var dir = new DirectoryInfo(filePath);
            CheckDir(dir);
            
            
            void CheckDir(DirectoryInfo directoryInfo)
            {
                
                var dirs= directoryInfo.GetDirectories();
                for (int i = 0; i < dirs.Length; i++)
                {
                    CheckDir(dirs[i]);
                }
                
                
                var files=directoryInfo.GetFiles("*.mp4");
                for (int i = 0; i < files.Length; i++)
                {
                    CheckFile(files[i]);
                }
                
              
            }

            void CheckFile(FileInfo fileInfo,bool isVideo=true)
            {
                //var dirName = Path.GetDirectoryName(fileInfo.FullName);
                var outName =Path.GetFileNameWithoutExtension(fileInfo.Name);
                
                var fileDir=Path.GetDirectoryName(fileInfo.FullName);
                
                if (!string.IsNullOrEmpty(fileDir))
                {
                    var muLu = fileDir.Split("High\\")[1];
                    var outDir = outPath + "/" + muLu + "/";
                    var outP=outDir+ outName+".Png";
                    
                    if (!isVideo)
                    {
                        outP = outDir + outName + ".Jpg";
                    }
                    
                    AddHeaderObfuscation(fileInfo.FullName,outP);
                }
            }
            
            AssetDatabase.Refresh();
        }
        
        
        
        // 简单的加密脚本：在文件头加 1024 字节的垃圾数据
        public static void AddHeaderObfuscation(string srcPath, string dstPath)
        {
            
            // 1. 生成 1KB 垃圾数据
            // byte[] junk = new byte[1024]; 
            // new System.Random().NextBytes(junk);
            var dirName = Path.GetDirectoryName(dstPath);
            var fileName = Path.GetFileNameWithoutExtension(dstPath);
            var paths = dirName.Split("StreamingAssets");
            var path = paths[0]+"StreamingAssets";
            var dir = paths[1].Split("\\").Where((s =>!string.IsNullOrEmpty(s) )).ToList();
            foreach (var t in dir)
            {
                path += "/"+Tool.GetMd5AsString(t);
            }
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            path += "/"+Tool.GetMd5AsString(fileName)+".Png";
            
            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }
            
            Debug.Log($"加密视频文件:{srcPath}-----输出目录{path}------原始路径{dstPath}");
            // 2. 读取原视频所有字节（注意：超大视频建议用流式读写，不要一次性 ReadAllBytes）
            using (FileStream fsOut = new FileStream(path, FileMode.Create))
            {
                //fsOut.Write(junk, 0, junk.Length); // 写入垃圾头
        
                using (FileStream fsIn = new FileStream(srcPath, FileMode.Open))
                {
                    fsIn.CopyTo(fsOut); // 拼接到后面
                }
            }
        }
    }
}