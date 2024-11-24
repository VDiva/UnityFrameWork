using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public static class DllLoad
    {
        //private static Dictionary<string, Assembly> _assemblies = new Dictionary<string, Assembly>();
        
        public static Assembly Load(string assemblyName)
        {
            var asset = ABLoad.LoadAsset<TextAsset>("HotUpdate", assemblyName);
            var assem = Assembly.Load(asset.bytes);
            return assem;
        }
        
        public static Type LoadType(string assemblyName,string typeName)
        {
            var asset = ABLoad.LoadAsset<TextAsset>("HotUpdate", assemblyName);
            var assem = Assembly.Load(asset.bytes);
            return assem.GetType(typeName);
        }

        public static Assembly GetHotUpdateDll()
        {
            return Load("HotUpdate.dll.bytes");
        }

        public static Type GetHoyUpdateDllType(string typeName)
        {
            return LoadType("HotUpdate.dll.bytes", typeName);
        }
    }
}