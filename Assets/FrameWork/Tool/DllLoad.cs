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
            
            var asset = AbLoad.LoadAsset<TextAsset>("dll", assemblyName);
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
            var asset = AbLoad.LoadAsset<TextAsset>("dll", assemblyName);
            var assem = Assembly.Load(asset.bytes);
            _assemblies.Add(assemblyName,assem);
            return assem.GetType(typeName);
        }
    }
}