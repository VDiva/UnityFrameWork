using System;
using System.Collections;
using System.IO;
using System.Net;
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
        public static void DownLoadAsset(string path,Action<float,float,string,string> progress,Action<byte[],string> data,Action<string> err)
        {
            MyLog.Log(path);
            long statrTime = Tool.ConvertDateTimep(DateTime.Now);
            long lenght = 1;
            RequestTool requestTool = RequestTool.Create(path, Methods.Get);
            requestTool.Send((byte[] bytes) =>
            {
                progress?.Invoke(1,0,GetFileSize(lenght),GetFileSize(lenght));
                data?.Invoke(bytes,Path.GetFileName(path));
            }, err);
            
            requestTool.Progress += ((f,s) =>
            {
                lenght = s;
                long curTime = Tool.ConvertDateTimep(DateTime.Now)-statrTime;
                float speed = GetFileSize((float)s*f / curTime);
                float pro = f;
                progress?.Invoke(pro,speed,GetFileSize(s*f)+"",GetFileSize(s));
            });
            //Mono.Instance.StartCoroutine(DownLoadAssetIEumerator(path,progress,data,err));
        }
        
        static IEnumerator DownLoadAssetIEumerator(string path,Action<float,float,string,string> progress,Action<byte[],string> data,Action<string> err)
        {
            
            using (UnityWebRequest uwr=UnityWebRequest.Get(path))
            {
                
                
                // HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(path);
                // request.Method = "HEAD";
                // HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                // if (response.StatusCode == HttpStatusCode.OK)
                // {
                //     
                //     lenght = response.ContentLength;
                //     Console.WriteLine(response.ContentLength);
                // }
                
                uwr.SendWebRequest();
                
                if (uwr.isHttpError|| uwr.isNetworkError)
                {
                    MyLog.Log(uwr.error);
                    err?.Invoke(uwr.error);
                    yield break;
                }

                long statrTime = Tool.ConvertDateTimep(DateTime.Now);
                
                long lenght=1;
                var value = GetPackSize(path);
                if (value==null)
                {
                    err?.Invoke("网络错误获取失败");
                    yield break;
                }
                
                lenght=(long)value;
                string fileSize = GetFileSize(lenght);
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
        
        
        public static string GetFileSize(int size)
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


        public static long? GetPackSize(string path)
        {
            MyLog.Log(path);
            long lenght=1;
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(path);
            request.Method = "HEAD";
            
            
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    lenght = response.ContentLength;
                    Console.WriteLine(response.ContentLength);
                }
                return lenght;
            }
            catch (Exception e)
            {
                MyLog.Log(e.Message);
                return null;
            }
            
        }
    }
}