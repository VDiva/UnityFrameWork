using System;
using System.IO;

#if HYBRIDCLR_INSTALLED
using HybridCLR.Editor.Commands;
#endif

using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    public class DllTool: UnityEditor.Editor
    {
#if HYBRIDCLR_INSTALLED
        [MenuItem("FrameWork/程序集/更新程序集")]
        public static void Update()
        {
            CompileDllCommand.CompileDllActiveBuildTarget();
            var dllCopyPath = Config.DllCopyPath;
            var dlls = Config.Dlls;
            var dllPath = Config.DllPath;
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
            
            ABEditor.AssetPackaged();
            AssetDatabase.Refresh();
        }

        [MenuItem("FrameWork/程序集/构建所有程序集")]
        public static void SpawnAll()
        {
            PrebuildCommand.GenerateAll();
        }
#endif
        


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