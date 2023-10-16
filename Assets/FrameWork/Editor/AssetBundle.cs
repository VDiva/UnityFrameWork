using System.Collections;
using System.IO;
using FrameWork.Coroutine;
using FrameWork.Global;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork.Editor
{
    public class AssetBundle: UnityEditor.Editor
    {
        
        [MenuItem("FrameWork/AB/CreatAssetBundle for Android")]
        public static void CreatAssetBundleAsAndroid()
        {
            if (!Directory.Exists(GlobalVariables.ABAsAndroid))
            {
                Directory.CreateDirectory(GlobalVariables.ABAsAndroid);
            }
            BuildPipeline.BuildAssetBundles(GlobalVariables.ABAsAndroid, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.Android);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Android Finish!");
        }

        [MenuItem("FrameWork/AB/CreatAssetBundle for IOS")]
        public static void BuildAllAssetBundlesAsIOS()
        {
           
            if (!Directory.Exists(GlobalVariables.ABAsIos))
            {
                Directory.CreateDirectory(GlobalVariables.ABAsIos);
            }
            BuildPipeline.BuildAssetBundles(GlobalVariables.ABAsIos, BuildAssetBundleOptions.CollectDependencies, BuildTarget.iOS);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("IOS Finish!");

        }


        [MenuItem("FrameWork/AB/CreatAssetBundle for Win")]
        public static void CreatPCAssetBundleAsWindows()
        {
            
            if (!Directory.Exists(GlobalVariables.ABAsWindows))
            {
                Directory.CreateDirectory(GlobalVariables.ABAsWindows);
            }
            BuildPipeline.BuildAssetBundles(GlobalVariables.ABAsWindows, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Windows Finish!");
        }


        [MenuItem("Assets/FrameWork/SetAB/Material")]
        public static void SetMaterialAb()
        {
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            ai.assetBundleName = GlobalVariables.MaterialABName;
            ai.assetBundleVariant = GlobalVariables.ABNameEnd;
        }
        
        [MenuItem("Assets/FrameWork/SetAB/UiPrefab")]
        public static void SetUiPrefabAb()
        {
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            ai.assetBundleName = GlobalVariables.UiABName;
            ai.assetBundleVariant = GlobalVariables.ABNameEnd;
        }
        
        [MenuItem("Assets/FrameWork/SetAB/ScriptPrefab")]
        public static void SetScriptPrefabAb()
        {
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            ai.assetBundleName = GlobalVariables.ScriptPrefabABName;
            ai.assetBundleVariant = GlobalVariables.ABNameEnd;
        }
        
        [MenuItem("Assets/FrameWork/SetAB/Screen")]
        public static void SetScreen()
        {
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            ai.assetBundleName = GlobalVariables.ScreenABName;
            ai.assetBundleVariant = GlobalVariables.ABNameEnd;
        }


        
        
    }
}