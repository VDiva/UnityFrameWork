#if !UNITY_EDITOR && UNITY_WEBGL
using System;
using System.Collections.Generic;

namespace UnityWebSocket
{
    public class WebSocket : IWebSocket
    {
        public string Address { get; private set; }
        public string[] SubProtocols { get; private set; }
        public WebSocketState ReadyState { get { return (WebSocketState)WebSocketManager.WebSocketGetState(instanceId); } }
        public int PendingSendCount => pendingRealtime.Count;
        public int BufferedSendBytes => WebSocketManager.WebSocketGetBufferedAmount(instanceId);
        const int RealtimeBytesPerFrame = 16 * 1024;
        const int RealtimePacketsPerFrame = 8;
        readonly LinkedList<PendingPacket> pendingRealtime = new LinkedList<PendingPacket>();
        readonly Dictionary<ulong, LinkedListNode<PendingPacket>> pendingByKey =
            new Dictionary<ulong, LinkedListNode<PendingPacket>>();

        sealed class PendingPacket
        {
            public ulong key;
            public byte[] data;
            public int queuedAt;
            public int maxAgeMs;
        }

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<MessageEventArgs> OnMessage;

        internal int instanceId = 0;

        public WebSocket(string address)
        {
            this.Address = address;
            AllocateInstance();
        }

        public WebSocket(string address, string subProtocol)
        {
            this.Address = address;
            this.SubProtocols = new string[] { subProtocol };
            AllocateInstance();
        }

        public WebSocket(string address, string[] subProtocols)
        {
            this.Address = address;
            this.SubProtocols = subProtocols;
            AllocateInstance();
        }

        internal void AllocateInstance()
        {
            instanceId = WebSocketManager.AllocateInstance(this.Address);
            Log($"Allocate socket with instanceId: {instanceId}");
            if (this.SubProtocols == null) return;
            foreach (var protocol in this.SubProtocols)
            {
                if (string.IsNullOrEmpty(protocol)) continue;
                Log($"Add Sub Protocol {protocol}, with instanceId: {instanceId}");
                int code = WebSocketManager.WebSocketAddSubProtocol(instanceId, protocol);
                if (code < 0)
                {
                    HandleOnError(GetErrorMessageFromCode(code));
                    break;
                }
            }
        }

        ~WebSocket()
        {
            Log($"Free socket with instanceId: {instanceId}");
            WebSocketManager.WebSocketFree(instanceId);
        }

        public void ConnectAsync()
        {
            Log($"Connect with instanceId: {instanceId}");
            WebSocketManager.Add(this);
            int code = WebSocketManager.WebSocketConnect(instanceId);
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void CloseAsync()
        {
            ClearRealtime();
            Log($"Close with instanceId: {instanceId}");
            int code = WebSocketManager.WebSocketClose(instanceId, (int)CloseStatusCode.Normal, "Normal Closure");
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void SendAsync(string text)
        {
            ClearRealtime();
            Log($"Send, type: {Opcode.Text}, size: {text.Length}");
            int code = WebSocketManager.WebSocketSendStr(instanceId, text);
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void SendAsync(byte[] data)
        {
            ClearRealtime();
            SendRawBinary(data);
        }

        void SendRawBinary(byte[] data)
        {
            Log($"Send, type: {Opcode.Binary}, size: {data.Length}");
            int code = WebSocketManager.WebSocketSend(instanceId, data, data.Length);
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        // 微信小游戏的 WebSocket 适配层不更新 bufferedAmount，因此位置先留在可覆盖的应用队列。
        public void SendLatestAsync(byte[] data, ulong key,
            System.Func<byte[], byte[], byte[]> merge = null, int maxAgeMs = 0)
        {
            if (maxAgeMs <= 0) { SendRawBinary(data); return; }
            if (pendingByKey.TryGetValue(key, out var node))
            {
                node.Value.data = merge == null ? data : merge(node.Value.data, data);
                node.Value.queuedAt = Environment.TickCount;
                node.Value.maxAgeMs = maxAgeMs;
                return;
            }
            if (pendingRealtime.Count >= 256) RemoveFirstRealtime();
            var packet = new PendingPacket { key = key, data = data,
                queuedAt = Environment.TickCount, maxAgeMs = maxAgeMs };
            pendingByKey.Add(key, pendingRealtime.AddLast(packet));
        }

        public void FlushLatest()
        {
            if (ReadyState != WebSocketState.Open) { ClearRealtime(); return; }
            if (BufferedSendBytes >= RealtimeBytesPerFrame) return;
            int sentBytes = 0, sentPackets = 0;
            while (pendingRealtime.First != null && sentPackets < RealtimePacketsPerFrame)
            {
                PendingPacket packet = pendingRealtime.First.Value;
                RemoveFirstRealtime();
                if (unchecked(Environment.TickCount - packet.queuedAt) > packet.maxAgeMs)
                    continue;
                if (sentBytes > 0 && sentBytes + packet.data.Length > RealtimeBytesPerFrame)
                {
                    pendingByKey.Add(packet.key, pendingRealtime.AddFirst(packet));
                    break;
                }
                int code = WebSocketManager.WebSocketSend(instanceId, packet.data, packet.data.Length);
                if (code < 0) { HandleOnError(GetErrorMessageFromCode(code)); break; }
                sentBytes += packet.data.Length;
                sentPackets++;
            }
        }

        void RemoveFirstRealtime()
        {
            LinkedListNode<PendingPacket> first = pendingRealtime.First;
            if (first == null) return;
            pendingByKey.Remove(first.Value.key);
            pendingRealtime.RemoveFirst();
        }

        void ClearRealtime()
        {
            pendingByKey.Clear();
            pendingRealtime.Clear();
        }

        internal void HandleOnOpen()
        {
            Log("OnOpen");
            OnOpen?.Invoke(this, new OpenEventArgs());
        }

        internal void HandleOnMessage(byte[] rawData)
        {
            Log($"OnMessage, type: {Opcode.Binary}, size: {rawData.Length}");
            OnMessage?.Invoke(this, new MessageEventArgs(Opcode.Binary, rawData));
        }

        internal void HandleOnMessageStr(string data)
        {
            Log($"OnMessage, type: {Opcode.Text}, size: {data.Length}");
            OnMessage?.Invoke(this, new MessageEventArgs(Opcode.Text, data));
        }

        internal void HandleOnClose(ushort code, string reason)
        {
            ClearRealtime();
            Log($"OnClose, code: {code}, reason: {reason}");
            OnClose?.Invoke(this, new CloseEventArgs(code, reason));
            WebSocketManager.Remove(instanceId);
        }

        internal void HandleOnError(string msg)
        {
            Log("OnError, error: " + msg);
            OnError?.Invoke(this, new ErrorEventArgs(msg));
        }

        internal static string GetErrorMessageFromCode(int errorCode)
        {
            switch (errorCode)
            {
                case -1: return "WebSocket instance not found.";
                case -2: return "WebSocket is already connected or in connecting state.";
                case -3: return "WebSocket is not connected.";
                case -4: return "WebSocket is already closing.";
                case -5: return "WebSocket is already closed.";
                case -6: return "WebSocket is not in open state.";
                case -7: return "Cannot close WebSocket, An invalid code was specified or reason is too long.";
                case -8: return "Not support buffer slice. ";
                default: return $"Unknown error code {errorCode}.";
            }
        }

        [System.Diagnostics.Conditional("UNITY_WEB_SOCKET_LOG")]
        static void Log(string msg)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            UnityEngine.Debug.Log($"[{time}][UnityWebSocket] {msg}");
        }
    }
}
#endif
