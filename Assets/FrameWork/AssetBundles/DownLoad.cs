using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using FrameWork.Coroutine;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork.AssetBundles
{
    public class DownLoad
    {
        static int num = 1024; //byte
        public static void DownLoadAsset(string path,Action<float,float,string,string> progress,Action<byte[],string> data)
        {
            Mono.Instance.StartCoroutine(DownLoadAssetIEumerator(path,progress,data));
        }

        static IEnumerator DownLoadAssetIEumerator(string path,Action<float,float,string,string> progress,Action<byte[],string> data)
        {
            using (UnityWebRequest uwr=UnityWebRequest.Get(path))
            {
                
                long lenght=1;
                
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(path);
                request.Method = "HEAD";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    lenght = response.ContentLength;
                    Console.WriteLine(response.ContentLength);
                }

                string fileSize = GetFileSize(lenght);
                uwr.SendWebRequest();
                
                if (uwr.isHttpError|| uwr.isNetworkError)
                {
                    Debug.Log(uwr.error);
                    yield break;
                }
                long statrTime = Tool.Tool.ConvertDateTimep(DateTime.Now);
                
                while (!uwr.isDone)
                {
                    long curTime = Tool.Tool.ConvertDateTimep(DateTime.Now)-statrTime;
                    float prog = uwr.downloadProgress;
                    
                    progress(prog,prog/curTime*1000,GetFileSize((long)(prog*lenght)),fileSize);
                    yield return null;
                }

                if (uwr.isDone)
                {
                    var downLoad = uwr.downloadHandler;
                    data(downLoad.data,Path.GetFileName(path));
                }
            }
        }
        
        
        public static string GetFileSize(long size)
        {
            if (size < num)
                return size + "B";
            if (size < Math.Pow(num, 2))
                return (size / num).ToString("f2") + "K"; //kb
            if (size < Math.Pow(num, 3))
                return (size / Math.Pow(num, 2)).ToString("f2") + "M"; //M
            if (size < Math.Pow(num, 4))
                return (size / Math.Pow(num, 3)).ToString("f2") + "G"; //G
            return (size / Math.Pow(num, 4)).ToString("f2") + "T"; //T
        }
        
    }
}