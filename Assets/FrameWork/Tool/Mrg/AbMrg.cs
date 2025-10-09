
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FrameWork
{
    public static class ABMrg
    {
        public static void LoadAsync<T>(string name,Action<T> handle)
        {
            Addressables.LoadAssetAsync<T>(name).Completed += (h =>
            {
                handle?.Invoke(h.Result);
            });
        }
        
        public static T Load<T>(string name)
        {
            var assetAsync=Addressables.LoadAssetAsync<T>(name);
            var t = assetAsync.WaitForCompletion();
            return (T)t;
        }

        public static void Release(object obj)
        {
            Addressables.Release(obj);
        }
    } 
}