using System;
using System.Threading.Tasks;
using Library.Msg;
using NetWorkServer.Mrg;
using Riptide;
using Riptide.Utils;

namespace NetWorkServer
{
    public static class NetServer
    {
        private static Server _server;
        
        /// <summary>
        /// 客户端连接时调用
        /// </summary>
        public static event EventHandler<ServerConnectedEventArgs> ClientConnected;
        
        /// <summary>
        /// 客户端连接失败时调用
        /// </summary>
        public static event EventHandler<ServerConnectionFailedEventArgs> ConnectionFailed;
        
        /// <summary>
        /// 收到消息时调用
        /// </summary>
        public static event EventHandler<MessageReceivedEventArgs> MessageReceived;
        
        /// <summary>
        /// 客户端断开时调用
        /// </summary>
        public static event EventHandler<ServerDisconnectedEventArgs> ClientDisconnected;

        public static void Start(ushort port, ushort maxConnections)
        {
            RoomMrg.Init();
            RiptideLogger.Initialize(Console.WriteLine,true);
            _server = new Server();
            _server.ConnectionFailed +=ConnectionFailed;
            _server.ClientConnected +=ClientConnected;
            _server.MessageReceived +=MessageReceived;
            _server.ClientDisconnected +=ClientDisconnected;
            _server.Start(port, maxConnections);
            Task.Run(RunTask);
        }


        public static async void RunTask()
        {
            while (true)
            {
                await Task.Delay(50);
                _server.Update();
                _server.SendToAll(MsgMrg.CreateMsg(MessageSendMode.Reliable, MsgType.Update));
            }
        }


        public static Connection? GetConnection(ushort id)
        {
            if (_server.TryGetClient(id, out Connection connection))
            {
                return connection;
            }

            return null;
        }
        
        public static void Send(Message msg,ushort[] clientId)
        {
            for (int i = 0; i < clientId.Length; i++)
            {
                _server.Send(msg, clientId[i]);
            }
        }
        
        public static void Send(Message msg,Connection[] connection)
        {
            for (int i = 0; i < connection.Length; i++)
            {
                _server.Send(msg, connection[i]);
            }
        } 

        /// <summary>
        /// 发送给指定客户端
        /// </summary>
        public static void Send(Message msg,ushort clientId)
        {
            _server.Send(msg, clientId);
        } 
        
        /// <summary>
        /// 发送给指定客户端
        /// </summary>
        public static void Send(Message msg,Connection connection)
        {
            _server.Send(msg,connection);
        } 
        
        /// <summary>
        /// 发送给全部客户端
        /// </summary>
        /// <param name="msg"></param>
        public static void SendToAll(Message msg)
        {
            _server.SendToAll(msg);
        } 
    }
}