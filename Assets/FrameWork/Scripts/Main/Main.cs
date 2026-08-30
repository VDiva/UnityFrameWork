
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FrameWork
{
    public class Main
    {
        [RuntimeInitializeOnLoadMethod]
        public static void Run()
        {
            Initialize().Forget();
        }

        private static async UniTaskVoid Initialize()
        {
            try
            {
                await ABMrg.Init();
                Debug.Log("Addressables 初始化完成");
                UiManager.Init();
                //ABMrg.LoadSceneAsync(nameof(SceneType.LoadScene));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
