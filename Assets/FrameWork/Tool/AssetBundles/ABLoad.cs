using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork
{
    public static class ABLoad
    {
        private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();
        //private static string abEndName = "info";
        public static T LoadAsset<T>(string packName,string name,bool isMd5=true) where T : Object
        {
            if(isMd5) packName = Tool.GetAbName(packName);
            AssetBundle assetBundle=null;
            //packName=GtePackName(packName,name);
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                string newPath = Application.streamingAssetsPath+Config.GetAbPath();
                if (File.Exists(newPath+packName+"."+Config.AbEndName))
                {
                    assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(newPath+packName+"."+Config.AbEndName),Config.Key));
                    _assetBundles.TryAdd(packName, assetBundle);
                }
                else
                {
                    string path = Application.streamingAssetsPath+Config.GetAbPath();
                    assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(path+packName+"."+Config.AbEndName),Config.Key));
                    _assetBundles.TryAdd(packName, assetBundle);
                }
            }
            
            var obj=assetBundle.LoadAsset<T>(name);
            return obj;
        }
        
        
        
        public static void LoadAssetAsync<T>(string packName,string name,bool isMd5,Action<T> action) where T : Object
        {
            if(isMd5) packName = Tool.GetAbName(packName);
            AssetBundle assetBundle=null;
            //packName=GtePackName(packName,name);
            if (Application.platform!=RuntimePlatform.WebGLPlayer)
            {
                if (!_assetBundles.TryGetValue(packName,out assetBundle))
                {
                    string newPath = Application.streamingAssetsPath+Config.GetAbPath();
                    if (File.Exists(newPath+packName+"."+Config.AbEndName))
                    {
                        assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(newPath+packName+"."+Config.AbEndName),Config.Key));
                        _assetBundles.TryAdd(packName, assetBundle);
                    }
                    else
                    {
                        string path = Application.streamingAssetsPath+Config.GetAbPath();
                        assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(path+packName+"."+Config.AbEndName),Config.Key));
                        _assetBundles.TryAdd(packName, assetBundle);
                    }
                }
                var asset=assetBundle.LoadAssetAsync<T>(name);
                asset.completed += (operation =>
                {
                    action((T)asset.asset);
                } );
            }
            else
            {   
                if (_assetBundles.TryGetValue(packName,out assetBundle))
                {
                    //string path = Tool.GetAbPath();
                    // assetBundle =AssetBundle.LoadFromFile(path+packName+"."+abEndName);;
                    // assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(path+packName+"."+abEndName),Config.key));
                    // _assetBundles.TryAdd(packName, assetBundle);
                    action?.Invoke(assetBundle.LoadAsset<T>(name));
               
                }
                else
                {
                    DownLoadAb(Application.persistentDataPath+packName, (bytes =>
                    {
                        var assetAb=AssetBundle.LoadFromMemory(bytes);
                        _assetBundles.TryAdd(packName, assetAb);
                        assetBundle = assetAb;
                        action?.Invoke(assetBundle.LoadAsset<T>(name));
                    }));
                }
            }
        }
        
        private static void DownLoadAb(string path,Action<byte[]> data)
        {
            RequestTool requestTool = new RequestTool(path, Methods.Get);
            requestTool.Send(((byte[] da) =>
            {
                data?.Invoke(da);
            } ));
        }

    }
}