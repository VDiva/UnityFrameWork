using System;
using System.Net;
using System.Net.Sockets;

namespace NetWork
{
    public class NetWorkSystem
    {
        private static System.Net.Sockets.Socket _socket;
        private static int _count;
        
        
        public static Client client;
        private static SocketAsyncEventArgs _accept;

        public static Action OpenServer;

        public static Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;
        public static Action<object, SocketAsyncEventArgs> acceptAction;
        private static int index=0;
        
        
        public static void NetAsClient(string ip,int port,int count)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream,ProtocolType.Tcp);
            client = new Client(_socket, count);
            _socket.Connect(ipEndPoint);
           
        }
        
        
        public static void NetAsServer(string ip,int port,int maxAccept,int count)
        {
            _accept = new SocketAsyncEventArgs();
            _accept.Completed += OnAcceptCompleted;
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream,ProtocolType.Tcp);
            _count = count;
            _socket.Bind(ipEndPoint);
            _socket.Listen(maxAccept);
            OpenServer?.Invoke();
            WaitAccept();
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
            SocketManager.Instance.AddClient(index,cli);
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