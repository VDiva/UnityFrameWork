
using System;
using UnityEngine;

#if ADDRESSABLESCN_INSTALLED
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif


namespace FrameWork
{
    public static class ABMrg
    {
        public static void LoadAsync<T>(string name,Action<T> handle)
        {
#if ADDRESSABLESCN_INSTALLED
            Addressables.LoadAssetAsync<T>(name).Completed += (h =>
            {
                handle?.Invoke(h.Result);
            });
#endif
            
        }
        
        public static T Load<T>(string name)
        {
#if ADDRESSABLESCN_INSTALLED
            var assetAsync=Addressables.LoadAssetAsync<T>(name);
            var t = assetAsync.WaitForCompletion();
            return (T)t;
#endif
            
            return default(T);
        }

        public static void Release(GameObject obj)
        {
#if ADDRESSABLESCN_INSTALLED
            Addressables.ReleaseInstance(obj);
#endif
            
        }
    } 
}