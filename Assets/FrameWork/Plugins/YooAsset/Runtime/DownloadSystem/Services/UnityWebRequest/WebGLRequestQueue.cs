#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace YooAsset
{
    /// <summary>Main-thread FIFO shared by all YooAsset WebGL HTTP requests.</summary>
    public static class WebGLRequestQueue
    {
        public static int MaxConcurrency = 4;
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int Framework_IsWeChatRuntime();
        private static bool? _isWeChat;
        private static bool IsWeChat => (_isWeChat ?? (_isWeChat = Framework_IsWeChatRuntime() != 0)).Value;
        private static readonly LinkedList<Ticket> Pending = new LinkedList<Ticket>();
        private static int _active;

        internal sealed class Ticket : IDisposable
        {
            private readonly UnityWebRequest _request;
            private LinkedListNode<Ticket> _node;
            private bool _activeSlot;
            private bool _disposed;
            internal UnityEngine.Networking.UnityWebRequestAsyncOperation Operation { get; private set; }
            internal Ticket(UnityWebRequest request)
            {
                _request = request;
                _node = Pending.AddLast(this);
            }
            internal bool TryStart()
            {
                if (_disposed) return false;
                if (Operation != null) return true;
                if (IsWeChat && (_active >= Math.Max(1, MaxConcurrency) || Pending.First != _node)) return false;
                Pending.Remove(_node);
                _node = null;
                _active++;
                _activeSlot = true;
                try
                {
                    Operation = _request.SendWebRequest();
                    Operation.completed += _ => ReleaseSlot();
                    if (Operation.isDone) ReleaseSlot();
                    return true;
                }
                catch { ReleaseSlot(); throw; }
            }
            private void ReleaseSlot()
            {
                if (!_activeSlot) return;
                _activeSlot = false;
                _active--;
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_node != null) { Pending.Remove(_node); _node = null; }
                // The owner disposes the UnityWebRequest before releasing an active ticket.
                ReleaseSlot();
            }
        }
        internal static Ticket Enqueue(UnityWebRequest request) => new Ticket(request);
    }
}
#endif
