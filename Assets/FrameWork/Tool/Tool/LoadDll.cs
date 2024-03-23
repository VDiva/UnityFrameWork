using System;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public static class LoadDll
    {
        public static Assembly Load(string assemblyName)
        {
            var asset = AssetBundlesLoad.LoadAsset<TextAsset>("dll", assemblyName);
            return Assembly.Load(asset.bytes);
        }
        
        public static Type LoadType(string assemblyName,string typeName)
        {
            var asset = AssetBundlesLoad.LoadAsset<TextAsset>("dll", assemblyName);
            return Assembly.Load(asset.bytes).GetType(typeName);
        }
    }
}