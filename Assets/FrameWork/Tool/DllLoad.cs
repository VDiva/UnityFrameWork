using System;
using System.Reflection;
using UnityEngine;

namespace FrameWork.Tool
{
    public static class DllLoad
    {
        public static Assembly Load(string assemblyName)
        {
            var asset = AbLoad.LoadAsset<TextAsset>("dll", assemblyName);
            return Assembly.Load(asset.bytes);
        }
        
        public static Type LoadType(string assemblyName,string typeName)
        {
            var asset = AbLoad.LoadAsset<TextAsset>("dll", assemblyName);
            return Assembly.Load(asset.bytes).GetType(typeName);
        }
    }
}