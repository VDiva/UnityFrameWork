using System;
using System.Collections;
using System.Text;
using FrameWork.Coroutine;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork.AssetBundles
{
    public class DownLoad
    {
        public static void DownLoadAbVersion(string path,Action<byte[]> action)
        {
            Mono.Instance.StartCoroutine(DownLoadAbVersionIEumerator(path,action));
        }

        static IEnumerator DownLoadAbVersionIEumerator(string path,Action<byte[]> action)
        {
            using (UnityWebRequest uwr=UnityWebRequest.Get(path))
            {
                yield return uwr.SendWebRequest();
                if (uwr.isHttpError|| uwr.isNetworkError)
                {
                    Debug.Log(uwr.error);
                }
                else
                {
                    byte[] data=uwr.downloadHandler.data;
                    action(data);
                }
            }
        }
        
        
        public static void DownLoadAb(string path,Action<AssetBundle> action)
        {
            Mono.Instance.StartCoroutine(DownLoadAbIEumerator(path,action));
        }

        static IEnumerator DownLoadAbIEumerator(string path,Action<AssetBundle> action)
        {
            using (UnityWebRequest uwr=UnityWebRequest.Get(path))
            {
                yield return uwr.SendWebRequest();
                if (uwr.isHttpError|| uwr.isNetworkError)
                {
                    Debug.Log(uwr.error);
                }
                else
                {
                    byte[] data=uwr.downloadHandler.data;
                }
            }
        }
    }
}