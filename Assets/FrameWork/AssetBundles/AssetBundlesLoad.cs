using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork
{
    public static class AssetBundlesLoad
    {
        private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();

        private static FrameWork.BuildTarget buildTarget;
        static AssetBundlesLoad()
        {
            RuntimePlatform platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                    buildTarget = BuildTarget.Windows;
                    break;
                case RuntimePlatform.WindowsPlayer:
                    buildTarget = BuildTarget.Windows;
                    break;
                case RuntimePlatform.Android:
                    buildTarget = BuildTarget.Android;
                    break;
                case RuntimePlatform.IPhonePlayer:
                    buildTarget = BuildTarget.Ios;
                    break;
            }
        }
        
        
        public static T LoadAsset<T>(string packName,string name) where T : Object
        {
            AssetBundle assetBundle;
            string path = "";
            
            switch (buildTarget)
            {
                case BuildTarget.Windows:
                    path = Application.streamingAssetsPath+"/StandaloneWindows";
                    break;
                case BuildTarget.Android:
                    path = Application.streamingAssetsPath+"/Android";
                    break;
                case BuildTarget.Ios:
                    path = Application.streamingAssetsPath+"/Ios";
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
                    //assetBundle=AssetBundle.LoadFromFile(Application.streamingAssetsPath+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
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
                    path = Application.dataPath+"/AssetBundles/StandaloneWindows";
                    break;
                case BuildTarget.Android:
                    path = Application.dataPath+"/AssetBundles/Android";
                    break;
                case BuildTarget.Ios:
                    path = Application.dataPath+"/AssetBundles/Ios";
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