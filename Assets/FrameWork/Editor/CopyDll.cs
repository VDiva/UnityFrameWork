using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    public class CopyDll: UnityEditor.Editor
    {
        private static string[] dlls = new String[] { "HotUpdate.dll" };
        private static string dataPath = Application.dataPath.Replace("Assets", "");
        private static string dllPath = dataPath+"\\HybridCLRData\\HotUpdateDlls\\";
        private static string dllCopyPath = dataPath+"\\Assets\\FrameWork\\Asset\\Dll\\";
        
        
        private static string GetAbPath()
        {
            string path = "";
            RuntimePlatform platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    path ="/StandaloneWindows64/";
                    break;
                case RuntimePlatform.Android:
                    path = "/Android/";
                    break;
                case RuntimePlatform.IPhonePlayer:
                    path = "/Ios/";
                    break;
            }

            return path;
        }
        
        
        [MenuItem("FrameWork/Dll/CopyDll")]
        public static void Copy()
        {
            
            if (!Directory.Exists(dllCopyPath))
            {
                Directory.CreateDirectory(dllCopyPath);
            }
            
            foreach (var item in dlls)
            {
                Debug.Log(dllPath+GetAbPath()+item);
                if (File.Exists(dllPath+GetAbPath()+item))
                {
                    File.Copy(dllPath+GetAbPath()+item,dllCopyPath+item+".bytes",true);
                    Debug.Log(item+"--CopyTo--Asset");
                }
            }
            
            AssetDatabase.Refresh();
        }
    }
}