using System;
using System.Collections.Generic;
using System.Text;
using BestHTTP;
using UnityEngine;

namespace FrameWork
{
    public class RequestTool
    {
        //private  HTTPRequest _httpRequest;

        //private static RequestTool _requestTool;

        private  HTTPRequest _httpRequest;

        private Action<string> _valueString;
        private Action<byte[]> _valueByte;
        private Action<Texture2D> _valueTexture;

        private Action<string> _err;
        private Action<HTTPRequest, long, long> _progress;
        public RequestTool(string url,HTTPMethods httpMethods)
        {
            
            _httpRequest = new HTTPRequest(new Uri(url), httpMethods,((request, response) =>
            {
                if (response!=null)
                {
                    _valueTexture?.Invoke(response.DataAsTexture2D);
                    _valueString?.Invoke(response.DataAsText);
                    _valueByte?.Invoke(response.Data);
                }
                else
                {
                    _err?.Invoke("请求失败");
                }
            }));

            _httpRequest.OnUploadProgress += OnUploadProgressDelegate;
        }

        private void OnUploadProgressDelegate(HTTPRequest originalRequest, long uploaded, long uploadLength)
        {
            _progress?.Invoke(originalRequest,uploaded,uploadLength);
        }
        
        public static RequestTool Create(string url,HTTPMethods httpMethods)
        {
            return new RequestTool(url,httpMethods);
        }

        public RequestTool AddField(string key,string value)
        {
            _httpRequest.AddField(key,value);
            return this;
        }
        
        public RequestTool AddField(string key,string value,Encoding encoding)
        {
            _httpRequest.AddField(key,value,encoding);
            return this;
        }
        
        public RequestTool AddField<T,TK>(Dictionary<T,TK> dictionary)
        {
            foreach (var item in dictionary)
            {
                _httpRequest.AddField(item.Key.ToString(),item.Value.ToString());
            }
            return this;
        }

        public RequestTool AddField<T,TK>(List<T> key,List<TK> value)
        {
            for (int i = 0; i < key.Count; i++)
            {
                _httpRequest.AddField(key[i].ToString(),value[i].ToString());
            }
            return this;
        }
        
        public RequestTool AddField<T,TK>(T[] key,TK[] value)
        {
            for (int i = 0; i < key.Length; i++)
            {
                _httpRequest.AddField(key[i].ToString(),value[i].ToString());
            }
            return this;
        }
        
        public RequestTool AddField(int key,int value)
        {
            _httpRequest.AddField(key.ToString(),value.ToString());
            return this;
        }
        
        public RequestTool AddField(string key,int value)
        {
            _httpRequest.AddField(key,value.ToString());
            return this;
        }
        
        public RequestTool AddField(int key,string value)
        {
            _httpRequest.AddField(key.ToString(),value);
            return this;
        }
        
        
        public RequestTool AddField(float key,float value)
        {
            _httpRequest.AddField(key.ToString(),value.ToString());
            return this;
        }
        
        public RequestTool AddField(float key,string value)
        {
            _httpRequest.AddField(key.ToString(),value);
            return this;
        }
        
        public RequestTool AddField(string key,float value)
        {
            _httpRequest.AddField(key,value.ToString());
            return this;
        }
        
        
        public RequestTool AddField(float key,int value)
        {
            _httpRequest.AddField(key.ToString(),value.ToString());
            return this;
        }
        
        public RequestTool AddField(int key,float value)
        {
            _httpRequest.AddField(key.ToString(),value.ToString());
            return this;
        }
        

        public void Send(Action<Texture2D> action=null,Action<HTTPRequest, long, long> progress=null,Action<string> err=null)
        {
            _valueTexture = action;
            _err = err;
            _httpRequest.Send();
        }
        
        public void Send(Action<byte[]> action=null,Action<HTTPRequest, long, long> progress=null,Action<string> err=null)
        {
            _valueByte = action;
            _err = err;
            _httpRequest.Send();
            
            
        }
        
        public void Send(Action<string> action=null,Action<HTTPRequest, long, long> progress=null,Action<string> err=null)
        {
            _valueString = action;
            _err = err;
            _httpRequest.Send();
        }
        

    }
}