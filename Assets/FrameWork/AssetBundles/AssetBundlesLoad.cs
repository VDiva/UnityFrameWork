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
            string path = "";

            switch (buildTarget)
            {
                case BuildTarget.Windows:
                    path = GlobalVariables.Configure.AbWindowsPath;
                    break;
                case BuildTarget.Android:
                    path = GlobalVariables.Configure.AbAndroidPath;
                    break;
                case BuildTarget.Ios:
                    path = GlobalVariables.Configure.AbIosPath;
                    break;
            }
            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
                    Debug.Log("从旧包"+packName+"加载:"+name);
                }
                _assetBundles.TryAdd(packName, assetBundle);
            }
            var obj=assetBundle.LoadAsset<T>(name);
            return obj;
        }
        
        public static void SavePack(byte[] data,string packName)
        {
            _assetBundles.TryAdd(packName, AssetBundle.LoadFromMemory(data));
        }
        
        
        public static void LoadAssetAsync<T>(string packName,string name,BuildTarget buildTarget,Action<T> action) where T : Object
        {
            AssetBundle assetBundle;
            string path = "";

            switch (buildTarget)
            {
                case BuildTarget.Windows:
                    path = GlobalVariables.Configure.AbWindowsPath;
                    break;
                case BuildTarget.Android:
                    path = GlobalVariables.Configure.AbAndroidPath;
                    break;
                case BuildTarget.Ios:
                    path = GlobalVariables.Configure.AbIosPath;
                    break;
            }
            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+packName+"."+GlobalVariables.Configure.AbEndName);
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