using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Msg;
using Riptide;
using Riptide.Utils;

namespace NetWorkClient
{
    public static class NetClient
    {

        public static Func<float> RunTime; 
        public static Action<ushort> JoinRoomAction; //加入房间
        public static Action<ushort> LeaveRoomAction; //离开房间
        public static Action<Message> RetransmissionAction; //房间消息
        public static Action<string> InputDataAction; //输入消息
        
        
        private static List<ushort> _clientIds = new List<ushort>();
        
        private static Client _client;
        private static ushort _roomId;
        
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
        
        public static void Retransmission(Message msg)
        {
            Send(msg,false);
        }
        
        public static ushort GetClientId()
        {
            return _client.Id;
        }
        
        public static List<ushort> GetClientIds()
        {
            return _clientIds;
        }
        
        public static Message GetMessageHasRoomId(MessageSendMode messageSendMode,Enum id)
        {
            var msg = MsgMrg.CreateMsg(messageSendMode, id);
            msg.AddUShort(_roomId);
            return msg;
        }

        [MessageHandler((ushort)RoomType.JoinRoom)]
        private static void JoinRoom(Message message)
        {
            _roomId=message.GetUShort();
            var clinetId = message.GetUShort();
            _clientIds.Add(clinetId);
            JoinRoomAction?.Invoke(clinetId);
            RiptideLogger.Log(LogType.Info, $"JoinRoom({_roomId})");
        }
        
        [MessageHandler((ushort)RoomType.LeaveRoom)]
        public static void LeaveRoom(Message msg)
        {
            var id=msg.GetUShort();
            LeaveRoomAction?.Invoke(id);
            if (id==_client.Id)
            {
                _clientIds.Clear();
            }
            RiptideLogger.Log(LogType.Info, $"LeaveRoom({id})");
        }
        
        [MessageHandler((ushort)RoomType.Retransmission)]
        private static void RetransmissionMsg(Message message)
        {
            RetransmissionAction?.Invoke(message);
        }
        
        [MessageHandler((ushort)RoomType.InputData)]
        private static void InputData(Message message)
        {
            InputDataAction?.Invoke(message.GetString());
        }
        
        public static void SenInputData(string json)
        {
            var msg=MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.InputData);
            msg.AddString(json);
            Send(msg);
        }
    }
}