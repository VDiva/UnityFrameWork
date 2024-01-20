
using System;
using System.Reflection;
using FrameWork.Tool;
using NetWork.Type;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using Newtonsoft.Json;

namespace NetWork.System
{
    public class NetWorkSystem
    {
        public static Action<ushort,int> OnPlayerJoinRoom;
        public static Action<ushort> OnPlayerLeftRoom;
        public static Action<string> OnJoinError;
        public static Action<string> OnInformation;
        public static Action<ushort,ushort, Vector3,Vector3> OnTransform;
        public static Action<ushort, ushort, string, Vector3, Vector3> OnInstantiate;
        
        public static Action<string,ushort,object[]> OnRpc;


        public static Action OnConnectToServer;
        public static Action OnDisConnectToServer;
        
        
        private static Client _client;
        private static ushort _id;
        public static ushort serverTick;

        
        public static void Start(string address)
        {
            _client = new Client();
            _client.Connected += OnConnect;
            _client.Disconnected += OnDisConnect;
            RiptideLogger.Initialize(Debug.Log, false);
            _client.Connect(address);
            var netWork = NetWork.Instance;
            
        }

        
        private static void OnDisConnect(object sender, EventArgs e)
        {
            OnDisConnectToServer?.Invoke();
            Debug.Log("断开服务器....");
        }

        private static void OnConnect(object sender, EventArgs e)
        {
            OnConnectToServer?.Invoke();
            Debug.Log("链接到服务器....客户端id为:"+_client.Id);
        }
        
        public static void UpdateMessage()
        {
            _client.Update();
        }

        public static Message CreateMessage(MessageSendMode sendMode,Enum id)
        {
            return Message.Create(sendMode, id);
        }
        
        public static Message CreateMessage(MessageSendMode sendMode,ushort id)
        {
            return Message.Create(sendMode, id);
        }

        public static void Send(Message message)
        {
            _client.Send(message);
        }


        public static void DisConnect()
        {
            _client.Disconnect();
        }


        public static void JoinRoom(int roomId)
        {
            Message msg = Message.Create(MessageSendMode.Reliable,ClientToServerMessageType.JoinRoom);
            msg.AddInt(roomId);
            Send(msg);
        }
        
        
        public static void CreateRoom(string roomName,int maxCount)
        {
            Message msg = Message.Create(MessageSendMode.Reliable,ClientToServerMessageType.CreateRoom);
            
            msg.AddString(roomName);
            msg.AddInt(maxCount);
            Send(msg);
        }
        
        public static void MatchingRoom(string roomName,int maxCount)
        {
            Message msg = Message.Create(MessageSendMode.Reliable,ClientToServerMessageType.MatchingRoom);
            msg.AddString(roomName);
            msg.AddInt(maxCount);
            Send(msg);
        }
        
        public static void LeftRoom()
        {
            Message msg = Message.Create(MessageSendMode.Reliable,ClientToServerMessageType.LeftRoom);
            Send(msg);
        }


        public static ushort GetClientId()
        {
            return _client.Id;
        }
        
        public static void Instantiate(string spawnName,Vector3 position,Vector3 rotation,bool isPlayer)
        {
            
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Instantiate);
            msg.AddString(spawnName);
            msg.AddVector3(position);
            msg.AddVector3(rotation);
            msg.AddBool(isPlayer);
            Send(msg);
        }
        
        public static void Rpc(string methodName,NetWorkSystemMono netWorkSystemMono,Rpc rpc,object[] param)
        {
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Rpc);
            msg.AddString(methodName);
            msg.AddUShort(netWorkSystemMono.GetId());
            msg.AddUShort((ushort)rpc);
            msg.AddString(JsonConvert.SerializeObject(param));
            Send(msg);
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.SyncTick)]
        private static void SyncTick(Message message)
        {
            serverTick = message.GetUShort();
        }
    }
}
