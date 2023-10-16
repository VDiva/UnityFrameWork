using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using FrameWork.NetManager.Component;
using FrameWork.Singleton;
using NetWork;
using UnityEngine;

namespace FrameWork.NetManager
{
    public static class NetManager
    {

        public static bool IsOnline;
        public static Action OpenServer;
        public static Action<byte[], object, SocketAsyncEventArgs> ReceiveAction;
        public static Action<object, SocketAsyncEventArgs,string> ReceiveErrAction;
        public static Action<object, SocketAsyncEventArgs> SendAction;

        public static Client Client;
        
        private static int _clientId;
        private static int _roomId;
        private static ConcurrentQueue<Identity> _identities=new ConcurrentQueue<Identity>();

        private static ConcurrentDictionary<int, GameObject> _netDictionary =
            new ConcurrentDictionary<int, GameObject>();

        
        
        

        public static void ConnectToServer(string ip,int port,int byteSize=2048)
        {
            NetWorkSystem.OpenServer += OpenServer;
            NetWorkSystem.OpenServer += ConnectToServerSuccess;
            NetWorkSystem.NetAsClient(ip, port, byteSize);
            Client=NetWorkSystem.client;
            
        }


        /// <summary>
        /// 链接成功
        /// </summary>
        private static void ConnectToServerSuccess()
        {
            Client.ReceiveSuccessAction += ReceiveAction;
            Client.ReceiveErrAction += ReceiveErrAction;
            Client.SendAction += SendAction;
        }
       
        
        
        /// <summary>
        /// 请求给同步物体分配id
        /// </summary>
        /// <param name="identity"></param>
        public static void RequestAllocationId(Identity identity)
        {
            _identities.Enqueue(identity);
        }

        /// <summary>
        /// 服务器分配id后给同步物体id赋值
        /// </summary>
        /// <param name="id"></param>
        public static void AllocationNetId(int id)
        {
            if (_identities.TryDequeue(out Identity identity))
            {
                identity.id = id;
            }
        }
        
        /// <summary>
        /// 给客户端分配id
        /// </summary>
        /// <param name="id"></param>
        public static void AllocationClientId(int id)
        {
            _clientId = id;
        }
        
        /// <summary>
        /// 给房间分配id
        /// </summary>
        /// <param name="id"></param>
        public static void AllocationRoomId(int id)
        {
            _roomId = id;
        }
        
        /// <summary>
        /// 同步物体被销毁删除服务器同步
        /// </summary>
        /// <param name="identity"></param>
        public static void Destroy(Identity identity)
        {
            
        }

    }
}