using System;
using FrameWork.Global;
using UnityEngine;
using Object = System.Object;

namespace FrameWork.AssetBundles
{
    public class LoadAbAsset
    {
        public static T LoadAssetAsPrefab<T>(string name) where T : UnityEngine.Object
        {
            return AssetBundlesLoad.LoadAsset<T>(GlobalVariables.ScriptPrefabABName,name);
        }
        
        public static T LoadAssetAsUiPrefab<T>(string name) where T : UnityEngine.Object
        {
            return AssetBundlesLoad.LoadAsset<T>(GlobalVariables.UiABName,name);
        }
        
        public static T LoadAssetAsMaterial<T>(string name) where T : UnityEngine.Object
        {
            return AssetBundlesLoad.LoadAsset<T>(GlobalVariables.MaterialABName,name);
        }
        
        public static T LoadAssetAsScreen<T>(string name) where T : UnityEngine.Object
        {
            return AssetBundlesLoad.LoadAsset<T>(GlobalVariables.ScreenABName,name);
        }
    }
}