using System;
using System.IO;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    public class DllTool: UnityEditor.Editor
    {
        
        [MenuItem("FrameWork/Dll/Update")]
        public static void Update()
        {
            
            CompileDllCommand.CompileDllActiveBuildTarget();
            
            
            var dllCopyPath = Config.dllCopyPath;
            var dlls = Config.dlls;
            var dllPath = Config.dllPath;
            var abPath = GetPath();
            if (!Directory.Exists(dllCopyPath))
            {
                Directory.CreateDirectory(dllCopyPath);
            }
            
            foreach (var item in dlls)
            {
                Debug.Log(dllPath+abPath+item);
                if (File.Exists(dllPath+abPath+item))
                {
                    File.Copy(dllPath+abPath+item,dllCopyPath+item+".bytes",true);
                    Debug.Log(item+"--CopyTo--Asset");
                }
            }
            
            AssetBundle.CreatAssetBundle();
            ABConfig.CreateConfig();
            AssetDatabase.Refresh();
        }

        [MenuItem("FrameWork/Dll/SpawnAll")]
        public static void SpawnAll()
        {
            PrebuildCommand.GenerateAll();
        }


        public static string GetPath()
        {
            var path = "";
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: 
                    path ="/StandaloneWindows64/";
                    break;
                case BuildTarget.Android:
                    path ="/Android/";
                    break;
                case BuildTarget.iOS:
                    path ="/Ios/";
                    break;
            }

            return path;
        }
    }
}