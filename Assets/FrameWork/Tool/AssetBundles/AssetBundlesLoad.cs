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
        private static string abEndName = "info";
        public static T LoadAsset<T>(string packName,string name) where T : Object
        {
            AssetBundle assetBundle=null;
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                string path = Application.streamingAssetsPath+Config.GetAbPath();
                //assetBundle =AssetBundle.LoadFromFile(path+packName+"."+abEndName);
                assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(path+packName+"."+abEndName),Config.key));
                _assetBundles.TryAdd(packName, assetBundle);
            }
            //MyLog.Log($"从包"+packName+"加载:"+name);
            var obj=assetBundle.LoadAsset<T>(name);
            //assetBundle.Unload(false);
            return obj;
        }
        
        
        
        public static void LoadAssetAsync<T>(string packName,string name,Action<T> action) where T : Object
        {
            string path = Application.streamingAssetsPath+Config.GetAbPath();
            AssetBundle assetBundle=null;
            if (Application.platform!=RuntimePlatform.WebGLPlayer)
            {
                if (!_assetBundles.TryGetValue(packName,out assetBundle))
                {
                    //string path = Tool.GetAbPath();
                    // assetBundle =AssetBundle.LoadFromFile(path+packName+"."+abEndName);;
                    assetBundle =AssetBundle.LoadFromMemory(Tool.Decrypt(File.ReadAllBytes(path+packName+"."+abEndName),Config.key));
                    _assetBundles.TryAdd(packName, assetBundle);
               
                }
                //MyLog.Log($"从包"+packName+"加载:"+name);
                var asset=assetBundle.LoadAssetAsync<T>(name);
                asset.completed += (operation =>
                {
                    action((T)asset.asset);
                    // assetBundle.Unload(false);
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
                    DownLoadAb(path, (bytes =>
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