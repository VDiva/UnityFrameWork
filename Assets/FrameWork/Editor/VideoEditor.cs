using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrameWork.Editor
{
    public class VideoEditor : UnityEditor.Editor
    {


        private static string _videoPath = $"/Assets/Video/Nor";
        private static string _outVideoPath = $"/Assets/StreamingAssets/{Tool.Encrypt("Video")}/Nor";

        
        private static string _videoPath4k = "/Assets/Video/High";
        private static string _outVideoPath4k = $"/Assets/StreamingAssets/{Tool.Encrypt("Video")}/High";

        
        [MenuItem("FrameWork/视频处理/加密视频文件并复制到目录下普通目录下")]
        public static void CopyVideo()
        {
            
            var path=Application.dataPath.Replace("Assets", "");
            var filePath= path + _videoPath;
            var outPath = path + _outVideoPath;

            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            
            
            
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

            void CheckFile(FileInfo fileInfo)
            {
                //var dirName = Path.GetDirectoryName(fileInfo.FullName);
                var outName =Tool.Encrypt(Path.GetFileNameWithoutExtension(fileInfo.Name));
                var fileDir=Path.GetDirectoryName(fileInfo.FullName);

                if (!string.IsNullOrEmpty(fileDir))
                {
                    var muLu = fileDir.Split("Nor")[1];

                    var outDir = outPath + "/" + muLu + "/";
                    var outP=outDir+ outName+".Png";


                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                   
                   
                   
                    if (!File.Exists(outP))
                    {
                        File.Create(outP).Close();
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

            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            
            
            
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

            void CheckFile(FileInfo fileInfo)
            {
                //var dirName = Path.GetDirectoryName(fileInfo.FullName);
                var outName =Tool.Encrypt(Path.GetFileNameWithoutExtension(fileInfo.Name));
                
                var fileDir=Path.GetDirectoryName(fileInfo.FullName);
                
                if (!string.IsNullOrEmpty(fileDir))
                {
                    var muLu = fileDir.Split("High")[1];
                    var outDir = outPath + "/" + muLu + "/";
                    var outP=outDir+ outName+".Png";
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                    if (!File.Exists(outP))
                    {
                        File.Create(outP).Close();
                    }
                    AddHeaderObfuscation(fileInfo.FullName,outP);
                }
            }
            
            AssetDatabase.Refresh();
        }
        
        
        
        // 简单的加密脚本：在文件头加 1024 字节的垃圾数据
        public static void AddHeaderObfuscation(string srcPath, string dstPath)
        {
            Debug.Log($"加密视频文件:{srcPath}-----输出目录{dstPath}");
            // 1. 生成 1KB 垃圾数据
            // byte[] junk = new byte[1024]; 
            // new System.Random().NextBytes(junk);

            // 2. 读取原视频所有字节（注意：超大视频建议用流式读写，不要一次性 ReadAllBytes）
            using (FileStream fsOut = new FileStream(dstPath, FileMode.Create))
            {
                //fsOut.Write(junk, 0, junk.Length); // 写入垃圾头
        
                using (FileStream fsIn = new FileStream(srcPath, FileMode.Open))
                {
                    fsIn.CopyTo(fsOut); // 拼接到后面
                }
            }
        }
    }
    
    
    // 打包回调脚本：自动处理「可拖拽、打包不包含、打包后恢复引用」的变量
    public class IgnoreBuildButDraggableProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // 回调优先级（确保先于其他打包逻辑执行，避免遗漏）
        public int callbackOrder => 100;

        // 关键：存储打包前的变量原始引用（持久化保存打包前的拖拽值，用于打包后恢复）
        private static Dictionary<object, Dictionary<FieldInfo, object>> _savedResourceReferences = new Dictionary<object, Dictionary<FieldInfo, object>>();

        #region 打包前：保存原始引用 → 临时清空 → 切断打包链
        public void OnPreprocessBuild(BuildReport report)
        {
            _savedResourceReferences.Clear(); // 清空上一次的缓存

            // 查找场景中所有可编辑的 MonoBehaviour 组件（包含你拖拽赋值的所有对象）
            MonoBehaviour[] allMonoBehaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            
            foreach (var mb in allMonoBehaviours)
            {
                // 过滤无效对象、内置持久化对象（避免修改Unity内置资源）
                if (mb == null || EditorUtility.IsPersistent(mb) || string.IsNullOrEmpty(mb.gameObject.scene.name))
                    continue;

                // 反射获取脚本中的所有字段（公有/私有、实例字段）
                FieldInfo[] fields = mb.GetType().GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );

                foreach (var field in fields)
                {
                    // 检查字段是否标记了 DraggableButNotBuild 特性
                    var targetAttribute = field.GetCustomAttribute<DraggableButNotBuildAttribute>();
                    if (targetAttribute == null)
                        continue;

                    // 1. 保存原始引用（关键：将你拖拽赋值的资源引用持久化存储在字典中）
                    if (!_savedResourceReferences.ContainsKey(mb))
                    {
                        _savedResourceReferences[mb] = new Dictionary<FieldInfo, object>();
                    }
                    _savedResourceReferences[mb][field] = field.GetValue(mb);

                    // 2. 临时清空字段引用（仅用于打包，切断Unity的打包引用检测链）
                    // 这个操作只在当前打包流程中生效，不会修改你编辑器中的持久化数据
                    field.SetValue(mb, null);
                }
            }
        }
        #endregion

        #region 打包后：自动恢复原始引用 → 不影响后续开发
        public void OnPostprocessBuild(BuildReport report)
        {
            // 遍历之前保存的原始引用，逐一还原给对应的变量
            foreach (var (mb, fieldValueDict) in _savedResourceReferences)
            {
                if (mb == null)
                    continue;

                foreach (var (field, originalValue) in fieldValueDict)
                {
                    // 关键：将打包前保存的拖拽引用还原回去
                    // 还原后，变量的引用和打包前完全一致，无需你手动重新拖拽
                    field.SetValue(mb, originalValue);
                }
            }

            _savedResourceReferences.Clear(); // 清空缓存，释放内存
        }
        #endregion
    }
}