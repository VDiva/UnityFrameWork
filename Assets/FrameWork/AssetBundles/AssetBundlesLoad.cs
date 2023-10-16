using System;
using System.Collections.Concurrent;
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
                assetBundle=AssetBundle.LoadFromFile(GlobalVariables.ABAsWindows+"/"+packName+"."+GlobalVariables.ABNameEnd);
                _assetBundles.TryAdd(packName, assetBundle);
            }
            var obj=assetBundle.LoadAsset<T>(name);
            return obj;
        }
    }
}