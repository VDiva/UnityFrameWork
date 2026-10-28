using System;
using System.Text;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork
{
    public enum Methods { Get, Post }

    public class Request
    {
        private readonly string _url;
        private readonly Methods _httpMethods;
        private readonly WWWForm _data = new WWWForm();
        private bool _hasFields;
        private string _jsonBody;
        /// <summary>0 表示使用 Unity 默认超时设置。</summary>
        public int TimeoutSeconds { get; set; }

        public Request(string url, Methods httpMethods)
        {
            _url = url;
            _httpMethods = httpMethods;
        }
        public static Request Create(string url, Methods httpMethods) => new Request(url, httpMethods);

        public void AddField(string key, object value)
        {
            if (_jsonBody != null) throw new InvalidOperationException("JSON 请求不能同时添加表单字段。");
            _data.AddField(key, SerializeJson(value));
            _hasFields = true;
        }

        public Request SetJsonBody(object body)
        {
            if (_httpMethods != Methods.Post) throw new InvalidOperationException("JSON 请求体需要使用 Post。");
            if (_hasFields) throw new InvalidOperationException("表单请求不能同时设置 JSON 请求体。");
            _jsonBody = SerializeJson(body);
            return this;
        }

        private static string SerializeJson(object value)
        {
            // 当前 LitJson 默认拒绝根级标量，表单字段和 JSON 请求体也需要支持数字、字符串及 null。
            var writer = new JsonWriter { Validate = false };
            JsonMapper.ToJson(value, writer);
            return writer.ToString();
        }

        private UnityWebRequest CreateWebRequest()
        {
            if (TimeoutSeconds < 0) throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds));
            UnityWebRequest request = null;
            try
            {
                if (_httpMethods == Methods.Get) request = UnityWebRequest.Get(_url);
                else if (_jsonBody == null) request = UnityWebRequest.Post(_url, _data);
                else
                {
                    request = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbPOST);
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(_jsonBody));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                }
                request.timeout = TimeoutSeconds;
                return request;
            }
            catch { request?.Dispose(); throw; }
        }

        /// <summary>请求本身自动释放；成功返回的 DownloadHandler 所有权交给调用方，需用 using 释放。</summary>
        public async UniTask<DownloadHandler> Send()
        {
            using var request = CreateWebRequest();
            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"HTTP 请求失败：{request.responseCode}，{request.error}");
            request.disposeDownloadHandlerOnDispose = false;
            return request.downloadHandler;
        }

        public async UniTask<byte[]> SendByte()
        {
            using var handler = await Send();
            return handler?.data;
        }
        public async UniTask<string> SendSrt()
        {
            using var handler = await Send();
            return handler?.text;
        }
        public async UniTask<Texture2D> SendTexture2D()
        {
            if (_httpMethods != Methods.Get || !Uri.TryCreate(_url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
            using var request = UnityWebRequestTexture.GetTexture(_url);
            if (TimeoutSeconds < 0) throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds));
            request.timeout = TimeoutSeconds;
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
