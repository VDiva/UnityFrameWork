using System;
using System.Collections.Concurrent;
using System.IO;
using FrameWork.Global;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork.AssetBundles
{
    public static class AssetBundlesLoad
    {
        private static ConcurrentDictionary<string, AssetBundle> _assetBundles=new ConcurrentDictionary<string, AssetBundle>();
        public static T LoadAsset<T>(string packName,string name) where T : Object
        {
            AssetBundle assetBundle;
            if (!_assetBundles.TryGetValue(packName,out assetBundle))
            {
                FileInfo fileInfo = new FileInfo(Application.persistentDataPath+"/"+packName+"."+GlobalVariables.ABNameEnd);
                Debug.Log(Application.persistentDataPath+"/"+packName);
                if (fileInfo.Exists)
                {
                    assetBundle=AssetBundle.LoadFromFile(Application.persistentDataPath+"/"+packName+"."+GlobalVariables.ABNameEnd);
                    Debug.Log("从新包"+packName+"加载:"+name);
                }
                else
                {
                    assetBundle=AssetBundle.LoadFromFile(GlobalVariables.ABAsWindows+"/"+packName+"."+GlobalVariables.ABNameEnd);
                    Debug.Log("从旧包"+packName+"加载:"+name);
                }
                _assetBundles.TryAdd(packName, assetBundle);
                
            }
            var obj=assetBundle.LoadAsset<T>(name);
            return obj;
        }
    }
}