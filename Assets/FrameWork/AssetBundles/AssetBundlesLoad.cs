using System;
using System.Collections.Concurrent;
using System.IO;
using FrameWork.Global;
using FrameWork.Type;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork.AssetBundles
{
    public static class AssetBundlesLoad
    {
        private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();
        public static T LoadAsset<T>(string packName,string name,BuildTarget buildTarget) where T : Object
        {
            AssetBundle assetBundle;
            string path = "AssetBundles/";

            switch (buildTarget)
            {
                case BuildTarget.Windows:
                    path += "StandaloneWindows";
                    break;
                case BuildTarget.Android:
                    path += "Android";
                    break;
                case BuildTarget.Ios:
                    path += "Ios";
                    break;
            }
            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+".info");
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+".info");
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+packName+".info");
                    Debug.Log("从旧包"+packName+"加载:"+name);
                }
                _assetBundles.TryAdd(packName, assetBundle);
            }
            var obj=assetBundle.LoadAsset<T>(name);
            return obj;
        }
        
        public static void LoadAssetAsync<T>(string packName,string name,BuildTarget buildTarget,Action<T> action) where T : Object
        {
            AssetBundle assetBundle;
            string path = "AssetBundles/";

            switch (buildTarget)
            {
                case BuildTarget.Windows:
                    path += "StandaloneWindows";
                    break;
                case BuildTarget.Android:
                    path += "Android";
                    break;
                case BuildTarget.Ios:
                    path += "Ios";
                    break;
            }
            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+".info");
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+".info");
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+packName+".info");
                    Debug.Log("从旧包"+packName+"加载:"+name);
                }
                _assetBundles.TryAdd(packName, assetBundle);
            }
            var asset=assetBundle.LoadAssetAsync<T>(name);
            asset.completed += (operation =>
            {
                action((T)asset.asset);
            } );

        }
    }
}