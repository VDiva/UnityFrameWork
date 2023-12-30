
using System;
using System.Net;
using System.Net.Sockets;
using NetWork.Enum;
namespace NetWork
{
    public class NetWorkSystem
    {
        /// <summary>
        /// 链接的socket
        /// </summary>
        private System.Net.Sockets.Socket _socket;

        /// <summary>
        /// 数据传输最大的大小
        /// </summary>
        private int _count;
       
        /// <summary>
        /// 客户端链接套接字只在tcp使用
        /// </summary>
        private SocketAsyncEventArgs _accept;

        /// <summary>
        /// 服务器开启成功回调
        /// </summary>
        public Action OpenServer;

        /// <summary>
        /// 消息接受成功回调只在tcp使用
        /// </summary>
        public Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;

        /// <summary>
        /// 客户端链接成功回调之在tcp使用
        /// </summary>
        public Action<object, SocketAsyncEventArgs> acceptAction;

        /// <summary>
        /// 客户端分配id只在tcp使用
        /// </summary>
        private int index=0;


        /// <summary>
        ///使用Tcp方式链接
        /// </summary>
        /// <param name="ip">地址</param>
        /// <param name="port">端口有</param>
        /// <param name="count">传输数据最大大小</param>
        /// <returns></returns>
        public Client NetAsClientTcp(string ip,int port,int count)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(ipEndPoint);
            var client = new Client(_socket, count);
            return client;
        }


        /// <summary>
        /// 使用udp方式链接
        /// </summary>
        /// <param name="ip">地址</param>
        /// <param name="port">端口有</param>
        /// <param name="count">传输数据最大大小</param>
        /// <returns></returns>
        public Client NetAsClientUdp(string ip, int port, int count)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(ipEndPoint);
            _socket.Connect(ipEndPoint);
            var client = new Client(_socket, count);
            return client;
        }


        /// <summary>
        /// 使用Tcp方式开启一个服务器
        /// </summary>
        /// <param name="ip">地址</param>
        /// <param name="port">端口</param>
        /// <param name="maxAccept">最大链接数量</param>
        /// <param name="count">传输数据最大大小</param>
        public void NetAsServerTcp(string ip,int port,int maxAccept,int count)
        {
            
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(ipEndPoint);
            _socket.Listen(maxAccept);
            _accept = new SocketAsyncEventArgs();
            _accept.Completed += OnAcceptCompleted;
            _count=count;
            WaitAccept();
            OpenServer?.Invoke();
        }

        /// <summary>
        /// 使用udp的方式开启一个服务器
        /// </summary>
        /// <param name="ip">地址</param>
        /// <param name="port">端口</param>
        /// <param name="count">传输数据最大大小</param>
        /// <returns></returns>
        public Client NetAsServerUdp(string ip, int port, int count)
        {

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _socket = new System.Net.Sockets.Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(ipEndPoint);
            Client client = new Client(_socket, count);
            OpenServer?.Invoke();
            return client;
        }


        /// <summary>
        /// 等待客户端链接
        /// </summary>
        private void WaitAccept()
        {
            bool success=_socket.AcceptAsync(_accept);
            if (!success)
            {
                SuccessConnect();
            }
        }
       
        /// <summary>
        /// 客户端链接成功
        /// </summary>
        void  SuccessConnect()
        {
            Client cli=new Client(_accept.AcceptSocket, _count) { ID = index };
            cli.ReceiveSuccessAction += ReceiveSuccessAction;
            _accept.AcceptSocket = null;
            acceptAction?.Invoke(_accept.AcceptSocket,_accept);
            index += 1;
            WaitAccept();
        }
        
        /// <summary>
        /// 客户端链接成功套接字回调
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        private void OnAcceptCompleted(object obj,SocketAsyncEventArgs args)
        {
            SuccessConnect();
        }

    }
}