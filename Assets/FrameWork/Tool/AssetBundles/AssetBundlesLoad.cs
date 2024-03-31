using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace FrameWork
{
    public static class AssetBundlesLoad
    {
        //private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();

        
        private static string abEndName = "info";
        public static T LoadAsset<T>(string packName,string name) where T : Object
        {
            string path = Application.persistentDataPath+Tool.GetAbPath();
            AssetBundle assetBundle = assetBundle=AssetBundle.LoadFromFile(path+packName+"."+abEndName);;
            MyLog.Log($"从包"+packName+"加载:"+name);
            var obj=assetBundle.LoadAsset<T>(name);
            assetBundle.Unload(false);
            return obj;
        }


        
        public static void LoadAssetAsync<T>(string packName,string name,Action<T> action) where T : Object
        {
            string path = Application.persistentDataPath+Tool.GetAbPath();
            AssetBundle assetBundle = assetBundle=AssetBundle.LoadFromFile(path+packName+"."+abEndName);;
            MyLog.Log($"从包"+packName+"加载:"+name);
            var asset=assetBundle.LoadAssetAsync<T>(name);
            asset.completed += (operation =>
            {
                action((T)asset.asset);
                assetBundle.Unload(false);
            } );

        }
    }
}