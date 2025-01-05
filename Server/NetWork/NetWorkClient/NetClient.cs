using System;
using Library.Msg;
using Riptide;

namespace NetWorkClient
{
    public static class NetClient
    {
        private static Client _client;
        
        
        /// <summary>
        /// 链接成功
        /// </summary>
        public static event EventHandler Connected;
        
        /// <summary>
        /// 链接失败
        /// </summary>
        public static event EventHandler<ConnectionFailedEventArgs> ConnectionFailed;
        
        /// <summary>
        /// 消息
        /// </summary>
        public static event EventHandler<MessageReceivedEventArgs> MessageReceived;
        
        /// <summary>
        /// 断开链接
        /// </summary>
        public static event EventHandler<DisconnectedEventArgs> Disconnected;
        
        // /// <summary>Invoked when another <i>non-local</i> client connects.</summary>
        // public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        // /// <summary>Invoked when another <i>non-local</i> client disconnects.</summary>
        // public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        

        public static void Start(string ip, int port)
        {
            _client = new Client();

            _client.Connected += Connected;
            _client.ConnectionFailed += ConnectionFailed;
            _client.MessageReceived += MessageReceived;
            _client.Disconnected += Disconnected;
            _client.Connect(ip+":"+port);
        }
        
        public static void Update()
        {
            _client?.Update();
        }
        
        public static void Send(Message message,bool reliable = true)
        {
            _client.Send(message,reliable);
        }
        
        public static void CreateRoom(string roomName, ushort maxPlayers)
        {
            var msg=MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.CreateRoom);
            msg.AddString(roomName);
            msg.AddUShort(maxPlayers);
            Send(msg);
        }
        
        public static void MatchingRoom()
        {
            var msg=MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.MatchingRoom);
            Send(msg);
        }
        
        public static void JoinRoom(ushort roomId)
        {
            var msg=MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.JoinRoom);
            msg.AddUShort(roomId);
            Send(msg);
        }
        
        public static void LeaveRoom()
        {
            var msg=MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.LeaveRoom);
            Send(msg);
        }
        
        public static void Retransmission(ushort roomId,Message msg)
        {
            Send(msg,false);
        }
    }
}