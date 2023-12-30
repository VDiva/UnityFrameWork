
using System;
using System.Net;
using System.Net.Sockets;
using NetWork.Enum;
namespace NetWork
{
    public class NetWorkSystem
    {
        private System.Net.Sockets.Socket _socket;
        private int _count;
       
        private SocketAsyncEventArgs _accept;

        public Action OpenServer;

        public Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;
        public Action<object, SocketAsyncEventArgs> acceptAction;
        private int index=0;
        
        
        public Client NetAsClient(string ip,int port,int count,ConnectType socketType)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            switch (socketType)
            {
                case ConnectType.Tcp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    
                    break; 
                case ConnectType.Udp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    _socket.Bind(ipEndPoint);
                    break;
            }
            
            _socket.Connect(ipEndPoint);
            var client = new Client(_socket, count);
            return client;
        }
        
        
        public Client NetAsServer(string ip,int port,int maxAccept,int count, ConnectType socketType)
        {
            
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Client client=null;

            switch (socketType)
            {
                case ConnectType.Tcp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    //_socket.Connect(ipEndPoint);
                    _socket.Bind(ipEndPoint);
                    _socket.Listen(maxAccept);
                    _accept = new SocketAsyncEventArgs();
                    _accept.Completed += OnAcceptCompleted;
                    WaitAccept();
                    break;
                case ConnectType.Udp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    _socket.Bind(ipEndPoint);
                    client = new Client(_socket, count);
                    break;
            }
            OpenServer?.Invoke();
            return client;
        }

        private void WaitAccept()
        {
            bool success=_socket.AcceptAsync(_accept);
            if (!success)
            {
                SuccessConnect();
            }
        }
       
        void  SuccessConnect()
        {
            Client cli=new Client(_accept.AcceptSocket, 2048) { ID = index };
            cli.ReceiveSuccessAction += ReceiveSuccessAction;
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