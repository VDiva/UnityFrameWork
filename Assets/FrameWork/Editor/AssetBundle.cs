
using System.IO;
using UnityEditor;
using UnityEngine;


namespace FrameWork
{
    public class AssetBundle: UnityEditor.Editor
    {
        
        
        [MenuItem("FrameWork/AB/CreatAssetBundle")]
        public static void CreatAssetBundle()
        {


            var path = Application.streamingAssetsPath + GetPath();
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.Delete(path,true);
                Directory.CreateDirectory(path);
            }
            
            BuildPipeline.BuildAssetBundles(path, GetOptions(), EditorUserBuildSettings.activeBuildTarget);
            AssetDatabase.Refresh();
            Debug.Log("AB Finish!");
        }
        
        
        // [MenuItem("FrameWork/AB/CreatAssetBundle for Android")]
        // public static void CreatAssetBundleAsAndroid()
        // {
        //     
        //     if (!Directory.Exists(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.Android)))
        //     {
        //         Directory.CreateDirectory(Application.streamingAssetsPath+"/Android");
        //     }
        //     else
        //     {
        //         Directory.Delete(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.Android),true);
        //         Directory.CreateDirectory(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.Android));
        //     }
        //     
        //     BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.Android), BuildAssetBundleOptions.UncompressedAssetBundle, UnityEditor.BuildTarget.Android);
        //     AssetDatabase.Refresh();
        //     Debug.Log("Android Finish!");
        // }
        //
        // [MenuItem("FrameWork/AB/CreatAssetBundle for IOS")]
        // public static void CreatPCAssetBundleAsIos()
        // {
        //    
        //     if (!Directory.Exists(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.IPhonePlayer)))
        //     {
        //         Directory.CreateDirectory(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.IPhonePlayer));
        //     }else
        //     {
        //         Directory.Delete(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.IPhonePlayer),true);
        //         Directory.CreateDirectory(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.IPhonePlayer));
        //     }
        //     
        //     
        //     BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.IPhonePlayer), BuildAssetBundleOptions.CollectDependencies, UnityEditor.BuildTarget.iOS);
        //     AssetDatabase.Refresh();
        //     Debug.Log("IOS Finish!");
        //
        // }
        //
        //
        // [MenuItem("FrameWork/AB/CreatAssetBundle for Win")]
        // public static void CreatPCAssetBundleAsWindows()
        // {
        //     
        //     if (!Directory.Exists(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.WindowsPlayer)))
        //     {
        //         Directory.CreateDirectory(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.WindowsPlayer));
        //     }else
        //     {
        //         Directory.Delete(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.WindowsPlayer),true);
        //         Directory.CreateDirectory(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.WindowsPlayer));
        //     }
        //     
        //     BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath+Config.GetAbPath(RuntimePlatform.WindowsPlayer), BuildAssetBundleOptions.None, UnityEditor.BuildTarget.StandaloneWindows64);
        //     AssetDatabase.Refresh();
        //     Debug.Log("Windows Finish!");
        // }
        
        // [MenuItem("FrameWork/AB/All")]
        // public static void CreateAll()
        // {
        //     CreatPCAssetBundleAsWindows();
        //     CreatAssetBundleAsAndroid();
        //     //CreatPCAssetBundleAsIos();
        // }



        public static BuildAssetBundleOptions GetOptions()
        {
            BuildAssetBundleOptions bundleOptions = BuildAssetBundleOptions.None;
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    bundleOptions = BuildAssetBundleOptions.None;
                    break;
                case BuildTarget.Android:
                    bundleOptions = BuildAssetBundleOptions.UncompressedAssetBundle;
                    break;
                case BuildTarget.iOS:
                    bundleOptions = BuildAssetBundleOptions.CollectDependencies;
                    break;
            }

            return bundleOptions;
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