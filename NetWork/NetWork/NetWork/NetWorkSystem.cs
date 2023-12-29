
using System;
using System.Net;
using System.Net.Sockets;
using NetWork.Enum;
namespace NetWork
{
    public class NetWorkSystem
    {
        private static System.Net.Sockets.Socket _socket;
        private static int _count;
       
        private static SocketAsyncEventArgs _accept;

        public static Action OpenServer;

        public static Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;
        public static Action<object, SocketAsyncEventArgs> acceptAction;
        private static int index=0;
        
        
        public static Client NetAsClient(string ip,int port,int count,ConnectType socketType)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            switch (socketType)
            {
                case ConnectType.Tcp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    
                    break; 
                case ConnectType.Udp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                   
                    break;
            }
            _socket.Bind(ipEndPoint);
            _socket.Connect(ipEndPoint);
            var client = new Client(_socket, count);
            return client;
        }
        
        
        public static Client NetAsServer(string ip,int port,int maxAccept,int count, ConnectType socketType)
        {
            _accept = new SocketAsyncEventArgs();
            _accept.Completed += OnAcceptCompleted;
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);


            switch (socketType)
            {
                case ConnectType.Tcp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _socket.Connect(ipEndPoint);
                    _socket.Listen(maxAccept);
                    WaitAccept();
                    break;
                case ConnectType.Udp:
                    _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    _socket.Bind(ipEndPoint);
                    break;
            }

            _count = count;
            OpenServer?.Invoke();


            return new Client(_socket, _count);
        }

        private static void WaitAccept()
        {
            bool success=_socket.AcceptAsync(_accept);
            if (!success)
            {
                SuccessConnect();
            }
        }
       
        static void  SuccessConnect()
        {
            Client cli=new Client(_accept.AcceptSocket, 2048) { ID = index };
            cli.ReceiveSuccessAction += ReceiveSuccessAction;
            _accept.AcceptSocket = null;
            acceptAction?.Invoke(_accept.AcceptSocket,_accept);
            index += 1;
            WaitAccept();
        }
        
        private static void OnAcceptCompleted(object obj,SocketAsyncEventArgs args)
        {
            SuccessConnect();
        }





    }
}