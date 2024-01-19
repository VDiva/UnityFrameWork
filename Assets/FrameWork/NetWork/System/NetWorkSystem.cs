
using System;
using System.Collections.Generic;
using System.Threading;
using NetWork.Type;
using Riptide;
using Riptide.Utils;
using UnityEngine;

namespace NetWork.System
{
    public class NetWorkSystem
    {
        public static Action<ushort,int> OnPlayerJoinRoom;
        public static Action<ushort> OnPlayerLeftRoom;
        public static Action<string> OnJoinError;
        public static Action<string> OnInformation;
        public static Action<ushort,ushort, Vector3> OnTransform;
        
        
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
            Debug.Log("断开服务器....");
        }

        private static void OnConnect(object sender, EventArgs e)
        {
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
        
        [MessageHandler((ushort)ServerToClientMessageType.SyncTick)]
        private static void SyncTick(Message message)
        {
            serverTick = message.GetUShort();
        }
        
        
        
        
    }
}
