using System;
using UnityWebSocket;

namespace FrameWork.Net.Transport
{
    /// <summary>
    /// 延迟编码的实时快照载荷：仅在实际出队写入套接字前编码一次；
    /// 若出队前被更新快照覆盖或超时丢弃，则直接内存合并/跳过，省去主线程无效序列化与反序列化。
    /// </summary>
    public interface IDeferredNetPayload
    {
        int EstimatedBytes { get; }
        byte[] Encode();
        IDeferredNetPayload MergeFrom(IDeferredNetPayload older);
    }

    /// <summary>
    /// 统一客户端传输层接口（WebSocket / TCP / KCP / TCP+KCP 双通道）。
    /// 所有事件回调均保证在 Unity 主线程派发。
    /// </summary>
    public interface INetTransport
    {
        string Address { get; }
        string TransportName { get; }
        bool IsRealtimeChannelActive { get; }
        WebSocketState ReadyState { get; }
        int PendingSendCount { get; }
        int BufferedSendBytes { get; }

        event EventHandler<OpenEventArgs> OnOpen;
        event EventHandler<CloseEventArgs> OnClose;
        event EventHandler<ErrorEventArgs> OnError;
        event EventHandler<MessageEventArgs> OnMessage;

        void ConnectAsync();
        void CloseAsync();
        void SendAsync(byte[] data);
        void SendAsync(string text);
        /// <summary>低延迟非覆写实时流（如语音帧）：双通道下优先走 KCP，不可用时自动回退到主通道。</summary>
        void SendRealtimeAsync(byte[] data);
        void SendLatestAsync(byte[] data, ulong key,
            Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0);
        void SendLatestDeferredAsync(IDeferredNetPayload payload, ulong key, int maxAgeMs = 0);
        void FlushLatest();
    }
}
