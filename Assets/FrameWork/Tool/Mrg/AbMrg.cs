using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using Object = UnityEngine.Object;

#if ADDRESSABLESCN_INSTALLED
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace FrameWork
{
    /// <summary>
    /// Addressables 资源获取
    /// </summary>
    public static class ABMrg
    {
        public static void LoadSceneAsync(string name,Action<float> progress=null,Action call=null,Action<Exception> failure=null)
        {
#if ADDRESSABLESCN_INSTALLED
            try
            { 
                Mono.Instance.StartCoroutine(Load());
                IEnumerator Load()
                {
                    UiManager.HideAllUi();
                    yield return null;
                    Debug.Log("加载场景:"+name);
                    AsyncOperationHandle<SceneInstance> operationHandle;
                    try { operationHandle = Addressables.LoadSceneAsync(name); }
                    catch (Exception exception) { failure?.Invoke(exception); yield break; }
                    while (!operationHandle.IsDone)
                    {
                        try { progress?.Invoke(operationHandle.PercentComplete); }
                        catch (Exception exception) { Debug.LogException(exception); }
                        yield return null;
                    }
                    if (operationHandle.Status != AsyncOperationStatus.Succeeded)
                    {
                        var error = operationHandle.OperationException ?? new Exception("场景加载失败:" + name);
                        Debug.LogException(error);
                        failure?.Invoke(error);
                        Addressables.Release(operationHandle);
                        yield break;
                    }
                    yield return null;
                    try { progress?.Invoke(1f); } catch (Exception exception) { Debug.LogException(exception); }
                    yield return null;
                    call?.Invoke();
                    Debug.Log("加载成功场景:"+name);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                failure?.Invoke(e);
            }
#else
            failure?.Invoke(new InvalidOperationException("Addressables 未启用"));
            return;
#endif
        }

        public static void LoadScene(string name)
        {
#if ADDRESSABLESCN_INSTALLED
            Addressables.LoadSceneAsync(name).WaitForCompletion();
#else
            return;
#endif
        }
        
        /// <summary>
        /// 异步加载资源。
        /// </summary>
        public static async UniTask<T> LoadAsync<T>(string name, bool isRelease = true)
        {
#if ADDRESSABLESCN_INSTALLED
            try
            {
                return await Addressables.LoadAssetAsync<T>(name);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return default;
            }
#else

            return default;
#endif
        }


        /// <summary>
        /// 实例化一个aa资源
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static async UniTask<GameObject> InstantiateAsync(string name)
        {
#if ADDRESSABLESCN_INSTALLED
            try
            {
                return await Addressables.InstantiateAsync(name);
            }
            catch (Exception e)
            {
                return null;
            }
#else
            return null;
#endif
        }
        
        /// <summary>
        /// 销毁一个aa资源 如果没有通过aa生成的直接销毁
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static void ReleaseInstantiate(GameObject g)
        {
#if ADDRESSABLESCN_INSTALLED
            try
            {
                var isSuc = Addressables.ReleaseInstance(g);
                if (!isSuc)
                {
                    Object.Destroy(g);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
#else

            
#endif
        }

        /// <summary>
        /// 同步加载兼容接口
        /// </summary>
        public static T Load<T>(string name, bool isRelease = true)
        {
#if ADDRESSABLESCN_INSTALLED
            try
            {
                var operationHandle=Addressables.LoadAssetAsync<T>(name);
                return operationHandle.WaitForCompletion();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return default;
            }
#else
            return default;
#endif
        }
        
        /// <summary>释放指定Handle。</summary>
        public static void Release(AsyncOperationHandle handle)
        {
#if ADDRESSABLESCN_INSTALLED
            Addressables.Release(handle);
#else
            
#endif
        }

        public static async UniTask Init()
        {
#if ADDRESSABLESCN_INSTALLED
            Debug.Log("Addressables 开始初始化，RuntimePath: " + Addressables.RuntimePath);
            var handle = Addressables.InitializeAsync(false);
            float deadline = Time.realtimeSinceStartup + 30f;

            // Do not await AsyncOperationHandle directly here. On some WeChat WebGL
            // runtimes the UniTask Addressables awaiter callback is not resumed even
            // though ResourceManager continues updating normally.
            while (!handle.IsDone)
            {
                if (Time.realtimeSinceStartup >= deadline)
                    throw new TimeoutException("Addressables 初始化超过30秒，RuntimePath: " + Addressables.RuntimePath);

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw handle.OperationException ?? new Exception("Addressables 初始化失败，状态: " + handle.Status);

            Debug.Log("Addressables Catalog 加载成功");
#else
            await UniTask.CompletedTask;
#endif
        }
    }
}
