using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public static class DllLoad
    {
        private static Dictionary<string, Assembly> _assemblies = new Dictionary<string, Assembly>();
        
        public static Assembly Load(string assemblyName)
        {

            if (_assemblies.TryGetValue(assemblyName,out var assembly))
            {
                return assembly;
            }
            
            var asset = AssetBundlesLoad.LoadAsset<TextAsset>("dll", assemblyName);
            var assem = Assembly.Load(asset.bytes);
            _assemblies.Add(assemblyName,assem);
            return assem;
        }
        
        public static Type LoadType(string assemblyName,string typeName)
        {
            
            if (_assemblies.TryGetValue(assemblyName,out var assembly))
            {
                return assembly.GetType(typeName);
            }
            var asset = AssetBundlesLoad.LoadAsset<TextAsset>("dll", assemblyName);
            var assem = Assembly.Load(asset.bytes);
            _assemblies.Add(assemblyName,assem);
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