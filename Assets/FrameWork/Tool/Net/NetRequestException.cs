using System;
using GameData;

namespace FrameWork.Net
{
    /// <summary>服务端已响应，但明确拒绝或无法完成当前请求。</summary>
    public sealed class NetRequestException : Exception
    {
        public string ErrorCode { get; }
        public Msg Response { get; }

        public NetRequestException(string errorCode, string message, Msg response = null)
            : base(message)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "SERVER_ERROR" : errorCode;
            Response = response;
        }
    }
}
