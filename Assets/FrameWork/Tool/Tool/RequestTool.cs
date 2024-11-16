using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork
{

    public enum Methods
    {
        Get,
        Post
    }
    
    public class RequestTool
    {
        
        private Action<string> _valueString;
        private Action<byte[]> _valueByte;
        private Action<string> _err;
        public Action<float,int> Progress;
        private string _url;
        private Methods _httpMethods;
        private Dictionary<object, object> _dic;
        public RequestTool(string url,Methods httpMethods)
        {
            _url = url;
            _httpMethods = httpMethods;
            _dic = new Dictionary<object, object>();
        }
        
        
        public static RequestTool Create(string url,Methods httpMethods)
        {
            return new RequestTool(url,httpMethods);
        }

        public RequestTool AddField(object key,object value)
        {
            _dic.Add(key,value);
            return this;
        }
        
        public void Send()
        {
            Mono.Instance.StartCoroutine(Request());
        }


        public void Send(Action<string> data,Action<string> err=null)
        {
            _valueString += data;
            _err += err;
            Mono.Instance.StartCoroutine(Request());
        }
        
        public void Send(Action<byte[]> data,Action<string> err=null)
        {
            _valueByte += data;
            _err += err;
            Mono.Instance.StartCoroutine(Request());
        }


        public void Send(Action<Texture2D> data, Action<string> err = null)
        {
            Mono.Instance.StartCoroutine(GetTexture());
            IEnumerator GetTexture()
            {
                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(_url))
                {
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_dic)));
                    www.SendWebRequest();
                    if (www.isHttpError|| www.isNetworkError)
                    {
                        err?.Invoke(www.error);
                        MyLog.LogError(www.error);
                        yield break;
                    }

                    var sizeStr=www.GetRequestHeader("Content-Length");
                    int size = 0;
                    if (!string.IsNullOrEmpty(sizeStr))
                    {
                        size = int.Parse(sizeStr);
                    }
                
                    while (!www.isDone)
                    {
                        Progress?.Invoke(www.downloadProgress,size);
                        yield return null;
                    }

                    yield return null;
                
                    if (www.isHttpError|| www.isNetworkError)
                    { 
                        err?.Invoke(www.error);
                        MyLog.LogError(www.error);
                        yield break;
                    }
                
                
                    if (www.isDone)
                    {
                        Progress?.Invoke(1,size);
                        data?.Invoke(((DownloadHandlerTexture)www.downloadHandler).texture);
                    }
                    
                }
            }
        }


        IEnumerator Request()
        {
            using (UnityWebRequest www=new UnityWebRequest(_url,_httpMethods==Methods.Get ? "GET" : "POST"))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_dic)));
                www.SendWebRequest();
                if (www.isHttpError|| www.isNetworkError)
                {
                    MyLog.LogError(www.error);
                    yield break;
                }

                var sizeStr=www.GetRequestHeader("Content-Length");
                int size = 0;
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    size = int.Parse(sizeStr);
                }
                
                while (!www.isDone)
                {
                    Progress?.Invoke(www.downloadProgress,size);
                    yield return null;
                }

                yield return null;
                
                if (www.isHttpError|| www.isNetworkError)
                {
                    MyLog.LogError(www.error);
                    yield break;
                }
                
                
                if (www.isDone)
                {
                    Progress?.Invoke(1,size);
                    _valueString?.Invoke(www.downloadHandler.text);
                    _valueByte?.Invoke(www.downloadHandler.data);
                }
                
            }
        }
    }
}