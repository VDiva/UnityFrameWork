using System;
using System.Collections;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork
{
    public class DownLoad
    {
        static int num = 1024; //byte
        
        
        
        /// <summary>
        /// 下载数据并提供下载速度和下载大小
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <param name="data"></param>
        public static void DownLoadAsset(string path,Action<float,float,string,string> progress,Action<byte[],string> data)
        {
            Mono.Instance.StartCoroutine(DownLoadAssetIEumerator(path,progress,data));
        }

        static IEnumerator DownLoadAssetIEumerator(string path,Action<float,float,string,string> progress,Action<byte[],string> data)
        {
            using (UnityWebRequest uwr=UnityWebRequest.Get(path))
            {
                
                long lenght=1;
                // HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(path);
                // request.Method = "HEAD";
                // HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                // if (response.StatusCode == HttpStatusCode.OK)
                // {
                //     
                //     lenght = response.ContentLength;
                //     Console.WriteLine(response.ContentLength);
                // }
                lenght=GetPackSize(path);

                string fileSize = GetFileSize(lenght);
                uwr.SendWebRequest();
                
                if (uwr.isHttpError|| uwr.isNetworkError)
                {
                    Debug.Log(uwr.error);
                    yield break;
                }
                long statrTime = Tool.ConvertDateTimep(DateTime.Now);
                
                while (!uwr.isDone)
                {
                    long curTime = Tool.ConvertDateTimep(DateTime.Now)-statrTime;
                    float prog = uwr.downloadProgress;

                    float speed = GetFileSize(prog * lenght / curTime);
                    progress(prog,(int)speed,GetFileSize((long)(prog*lenght)),fileSize);
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
        
        
        public static float GetFileSize(float size)
        {
            if (size < num)
                return size;
            if (size < Math.Pow(num, 2))
                return (size / num);
            if (size < Math.Pow(num, 3))
                return (float)(size / Math.Pow(num, 2));
            if (size < Math.Pow(num, 4))
                return (float)(size / Math.Pow(num, 3));
            return (float)(size / Math.Pow(num, 4));
        }


        public static long GetPackSize(string path)
        {
            Debug.Log(path);
            long lenght=1;
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(path);
            request.Method = "HEAD";
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            
            if (response.StatusCode == HttpStatusCode.OK)
            {
                lenght = response.ContentLength;
                Console.WriteLine(response.ContentLength);
            }
            return lenght;
            
        }
        
        // public static void GetPackSize(string path,Action<long> lenght)
        // {
        //     Mono.Instance.StartCoroutine(GetPackSizeIEnumerator(path, lenght));
        // }

        // public static IEnumerator GetPackSizeIEnumerator(string path,Action<long> lenght)
        // {
        //     HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(path);
        //     request.Method = "HEAD";
        //     HttpWebResponse response = (HttpWebResponse)request.GetResponse();
        //     while (true)
        //     {
        //         if (response.StatusCode == HttpStatusCode.OK)
        //         {
        //             lenght(response.ContentLength);
        //             Debug.Log(response.ContentLength);
        //             yield break;
        //         }
        //         yield return 0;
        //         
        //     }
        // }
        
    }
}