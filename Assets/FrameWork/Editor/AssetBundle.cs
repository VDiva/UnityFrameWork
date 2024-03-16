
using System.IO;
using UnityEditor;
using UnityEngine;


namespace FrameWork
{
    public class AssetBundle: UnityEditor.Editor
    {
        
        [MenuItem("FrameWork/AB/CreatAssetBundle for Android")]
        public static void CreatAssetBundleAsAndroid()
        {
            
            if (!Directory.Exists(Application.streamingAssetsPath+"/Android"))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath+"/Android");
            }
            else
            {
                Directory.Delete(Application.streamingAssetsPath+"/Android",true);
                Directory.CreateDirectory(Application.streamingAssetsPath+"/Android");
            }
            
            BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath+"/Android", BuildAssetBundleOptions.UncompressedAssetBundle, UnityEditor.BuildTarget.Android);
            AssetDatabase.Refresh();
            Debug.Log("Android Finish!");
        }

        [MenuItem("FrameWork/AB/CreatAssetBundle for IOS")]
        public static void BuildAllAssetBundlesAsIOS()
        {
           
            if (!Directory.Exists(Application.streamingAssetsPath+"/Ios"))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath+"/Ios");
            }else
            {
                Directory.Delete(Application.streamingAssetsPath+"/Ios",true);
                Directory.CreateDirectory(Application.streamingAssetsPath+"/Ios");
            }
            
            
            BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath+"/Ios", BuildAssetBundleOptions.CollectDependencies, UnityEditor.BuildTarget.iOS);
            AssetDatabase.Refresh();
            Debug.Log("IOS Finish!");

        }


        [MenuItem("FrameWork/AB/CreatAssetBundle for Win")]
        public static void CreatPCAssetBundleAsWindows()
        {
            
            if (!Directory.Exists(Application.streamingAssetsPath+"/StandaloneWindows"))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath+"/StandaloneWindows");
            }else
            {
                Directory.Delete(Application.streamingAssetsPath+"/StandaloneWindows",true);
                Directory.CreateDirectory(Application.streamingAssetsPath+"/StandaloneWindows");
            }
            
            BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath+"/StandaloneWindows", BuildAssetBundleOptions.None, UnityEditor.BuildTarget.StandaloneWindows64);
            AssetDatabase.Refresh();
            Debug.Log("Windows Finish!");
        }


        // [MenuItem("Assets/FrameWork/SetAB/Material")]
        // public static void SetMaterialAb()
        // {
        //     AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        //     ai.assetBundleName = GlobalVariables.Configure.AbMaterialName;
        //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
        // }
        //
        // [MenuItem("Assets/FrameWork/SetAB/UiPrefab")]
        // public static void SetUiPrefabAb()
        // {
        //     AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        //     ai.assetBundleName = GlobalVariables.Configure.AbUiPrefabName;
        //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
        // }
        //
        // [MenuItem("Assets/FrameWork/SetAB/Mode")]
        // public static void SetModePrefabAb()
        // {
        //     AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        //     ai.assetBundleName = GlobalVariables.Configure.AbModePrefabName;
        //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
        // }
        //
        // [MenuItem("Assets/FrameWork/SetAB/Screen")]
        // public static void SetScreen()
        // {
        //     AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        //     ai.assetBundleName = GlobalVariables.Configure.AbScreenName;
        //     ai.assetBundleVariant = GlobalVariables.Configure.AbEndName;
        // }


        
        
    }
}