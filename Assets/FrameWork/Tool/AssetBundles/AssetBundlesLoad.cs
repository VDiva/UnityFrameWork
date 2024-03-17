using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork
{
    public static class AssetBundlesLoad
    {
        private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();

        


        private static string abEndName = "info";
        public static T LoadAsset<T>(string packName,string name) where T : Object
        {
            
            AssetBundle assetBundle;
            string path = Application.streamingAssetsPath+Config.GetAbPath();
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+abEndName);
                MyLog.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+abEndName);
                    MyLog.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+"/"+packName+"."+abEndName);
                    //assetBundle=AssetBundle.LoadFromFile(Application.streamingAssetsPath+"/"+packName+"."+GlobalVariables.Configure.AbEndName);
                    MyLog.Log("从旧包"+packName+"加载:"+name);
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
            string path = Application.streamingAssetsPath+Config.GetAbPath();


            
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+abEndName);
                MyLog.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+abEndName);
                    MyLog.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(path+packName+"."+abEndName);
                    MyLog.Log("从旧包"+packName+"加载:"+name);
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