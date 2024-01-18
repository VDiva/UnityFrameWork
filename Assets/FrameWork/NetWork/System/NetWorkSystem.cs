
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
        private static Client _client;
        private static ushort _id;
        private static long _serverTick;
        
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

        [MessageHandler((ushort)ServerToClientMessageType.SyncTick)]
        private static void SyncTick(Message message)
        {
            _serverTick = message.GetUShort();
            Debug.Log(_serverTick);
        }
        
    }
}
