using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork.Tool
{
    public static class AbLoad
    {
        private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();

        
        private static string GetAbPath()
        {
            string path = "";
            RuntimePlatform platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    path = Application.streamingAssetsPath + "/StandaloneWindows";
                    break;
                case RuntimePlatform.Android:
                    path = Application.streamingAssetsPath + "/Android";
                    break;
                case RuntimePlatform.IPhonePlayer:
                    path = Application.streamingAssetsPath + "/Ios";
                    break;
            }

            return path;
        }


        private static string abEndName = "info";
        public static T LoadAsset<T>(string packName,string name) where T : Object
        {
            
            AssetBundle assetBundle;
            string path = GetAbPath();
           
            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+abEndName);
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+abEndName);
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+"/"+packName+"."+abEndName);
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
        
        
        public static void LoadAssetAsync<T>(string packName,string name,Action<T> action) where T : Object
        {
            AssetBundle assetBundle;
            string path = GetAbPath();
            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+abEndName);
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+abEndName);
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+packName+"."+abEndName);
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