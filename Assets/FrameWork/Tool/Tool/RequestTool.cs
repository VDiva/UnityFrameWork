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
        private Dictionary<string, string> _dic;
        public RequestTool(string url,Methods httpMethods)
        {
            _url = url;
            _httpMethods = httpMethods;
            _dic = new Dictionary<string, string>();
        }
        
        
        public static RequestTool Create(string url,Methods httpMethods)
        {
            return new RequestTool(url,httpMethods);
        }

        public RequestTool AddField(string key,string value)
        {
            _dic.Add(key,value);
            return this;
        }
        
        public RequestTool AddField<T,TK>(Dictionary<T,TK> dictionary)
        {
            foreach (var item in dictionary)
            {
                AddField(item.Key.ToString(),item.Value.ToString());
            }
            return this;
        }

        public RequestTool AddField<T,TK>(List<T> key,List<TK> value)
        {
            for (int i = 0; i < key.Count; i++)
            {
                AddField(key[i].ToString(),value[i].ToString());
            }
            return this;
        }
        
        public RequestTool AddField<T,TK>(T[] key,TK[] value)
        {
            for (int i = 0; i < key.Length; i++)
            {
                AddField(key[i].ToString(),value[i].ToString());
            }
            return this;
        }
        
        public RequestTool AddField(int key,int value)
        {
            AddField(key.ToString(),value.ToString());
            return this;
        }
        
        public RequestTool AddField(string key,int value)
        {
            AddField(key,value.ToString());
            return this;
        }
        
        public RequestTool AddField(int key,string value)
        {
            AddField(key.ToString(),value);
            return this;
        }
        
        
        public RequestTool AddField(float key,float value)
        {
            AddField(key.ToString(),value.ToString());
            return this;
        }
        
        public RequestTool AddField(float key,string value)
        {
            AddField(key.ToString(),value);
            return this;
        }
        
        public RequestTool AddField(string key,float value)
        {
            AddField(key,value.ToString());
            return this;
        }
        
        
        public RequestTool AddField(float key,int value)
        {
            AddField(key.ToString(),value.ToString());
            return this;
        }
        
        public RequestTool AddField(int key,float value)
        {
            AddField(key.ToString(),value.ToString());
            return this;
        }


        public void Send()
        {
            if (_httpMethods.Equals(Methods.Get))
            {
                Mono.Instance.StartCoroutine(Get());
            }
            else
            {
                Mono.Instance.StartCoroutine(Post());
            }
        }


        public void Send(Action<string> data,Action<string> err=null)
        {
            _valueString += data;
            _err += err;
            if (_httpMethods.Equals(Methods.Get))
            {
                Mono.Instance.StartCoroutine(Get());
            }
            else
            {
                Mono.Instance.StartCoroutine(Post());
            }
        }
        
        public void Send(Action<byte[]> data,Action<string> err=null)
        {
            _valueByte += data;
            _err += err;
            if (_httpMethods.Equals(Methods.Get))
            {
                Mono.Instance.StartCoroutine(Get());
            }
            else
            {
                Mono.Instance.StartCoroutine(Post());
            }
        }


        IEnumerator Get()
        {
            using (UnityWebRequest request=UnityWebRequest.Get(_url))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_dic)));
                request.SendWebRequest();
                if (request.isHttpError|| request.isNetworkError)
                {
                    MyLog.LogError(request.error);
                    yield break;
                }

                var sizeStr=request.GetRequestHeader("Content-Length");
                int size = 0;
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    size = int.Parse(sizeStr);
                }
                
                while (!request.isDone)
                {
                    Progress?.Invoke(request.downloadProgress,size);
                    yield return null;
                }

                yield return null;
                if (request.isDone)
                {
                    Progress?.Invoke(1,size);
                    _valueString?.Invoke(request.downloadHandler.text);
                    _valueByte?.Invoke(request.downloadHandler.data);
                }
                
            }
        }
        
        
        IEnumerator Post()
        {
            using (UnityWebRequest request=UnityWebRequest.Post(_url,_dic))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                request.SendWebRequest();
                if (request.isHttpError|| request.isNetworkError)
                {
                    MyLog.LogError(request.error);
                    yield break;
                }
                var sizeStr=request.GetRequestHeader("Content-Length");
                int size = 0;
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    size = int.Parse(sizeStr);
                }
                while (!request.isDone)
                {
                    Progress?.Invoke(request.downloadProgress,size);
                    yield return null;
                }

                yield return null;
                if (request.isDone)
                {
                    Progress?.Invoke(1,size);
                    _valueString?.Invoke(request.downloadHandler.text);
                    _valueByte?.Invoke(request.downloadHandler.data);
                }
            }
        }

    }
}