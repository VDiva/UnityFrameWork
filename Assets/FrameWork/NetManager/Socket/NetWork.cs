using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
namespace FrameWork.NetManager.Socket
{
    public class NetWork
    {
        private System.Net.Sockets.Socket _socket;

        
        
        public Client client;
        private SocketAsyncEventArgs _accept;

        public Action OpenServer;

        public Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;
        public Action<object, SocketAsyncEventArgs> acceptAction;
        private int index=0;
        public NetWork()
        {
            _accept = new SocketAsyncEventArgs();
            _accept.Completed += OnAcceptCompleted;
        }
        
        public void NetAsClient(string ip,int port,int count)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream,ProtocolType.Tcp);
            client = new Client(_socket, count);
            _socket.Connect(ipEndPoint);
           
        }
        
        
        public void NetAsServer(string ip,int port,int maxAccept,int count)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream,ProtocolType.Tcp);
            client = new Client(_socket, count);
            _socket.Bind(ipEndPoint);
            _socket.Listen(maxAccept);
            OpenServer?.Invoke();
            WaitAccept();
            
        }

        private void WaitAccept()
        {
            bool success=_socket.AcceptAsync(_accept);
            if (!success)
            {
                SuccessConnect();
            }
        }
        
        
        void SuccessConnect()
        {
            Client cli=new Client(_accept.AcceptSocket, 2048) { ID = index };
            cli.ReceiveSuccessAction += ReceiveSuccessAction;
            SocketManager.Instance.AddClient(index,cli);
            _accept.AcceptSocket = null;
            acceptAction?.Invoke(_accept.AcceptSocket,_accept);
            index += 1;
            WaitAccept();
        }
        
        private void OnAcceptCompleted(object obj,SocketAsyncEventArgs args)
        {
            SuccessConnect();
        }
    }
}