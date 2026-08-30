using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork
{

    public enum Methods
    {
        Get,
        Post
    }
    
    //http发送一个对象
    public class Request
    {
        private string _url;
        private Methods _httpMethods;
        private WWWForm _data;
        //private Dictionary<object, object> _dic;
        public Request(string url,Methods httpMethods)
        {
            _url = url;
            _httpMethods = httpMethods;
            _data = new WWWForm();
            //_dic = new Dictionary<object, object>();
        }
        
        
        public static Request Create(string url,Methods httpMethods)
        {
            return new Request(url,httpMethods);
        }

        public void AddField(string key,object value)
        {
            _data.AddField(key,JsonMapper.ToJson(value));
        }

        public async UniTask<DownloadHandler> Send()
        {
            UnityWebRequest www;
            if (_httpMethods==Methods.Post)
            {
                www=UnityWebRequest.Post(_url,_data);
            }
            else
            {
                www=UnityWebRequest.Get(_url);
            }
            www.SetRequestHeader("Content-Type", "application/json");
            await www.SendWebRequest();
            return www.downloadHandler;
        }


        public async UniTask<byte[]> SendByte()
        {
            var data=await Send();
            if (data!=null)
            {
                return data.data;
            }

            return null;
        }
        
        
        public async UniTask<string> SendSrt()
        {
            var data=await Send();
            if (data!=null)
            {
                return data.text;
            }

            return null;
        }
        
        public async UniTask<Texture2D> SendTexture2D()
        {
            if (_httpMethods != Methods.Get ||
                !Uri.TryCreate(_url, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return null;

            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(_url);
            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"头像下载失败: {request.error}, url={_url}");
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }
}
