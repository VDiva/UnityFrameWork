
using System;
using System.Reflection;
using NetWork.Type;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using Newtonsoft.Json;

namespace FrameWork
{
    public class NetWorkSystem
    {
        private static Client _client;
        private static ushort _id;
        public static ushort serverTick;
        private static string _address;
        private static NetWork _netWork;
        public static void Start(string address)
        {
            _client = new Client();
            _client.Connected += OnConnect;
            _client.Disconnected += OnDisConnect;
            RiptideLogger.Initialize(MyLog.Log, false);
            _client.Connect(address);
            _netWork= NetWork.Instance;
            _address = address;
        }
        
        private static void OnDisConnect(object sender, EventArgs e)
        {
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.DisConnectToServer);
            //OnDisConnectToServer?.Invoke();
            MyLog.Log("断开服务器....");
            Start(_address);
            
        }

        private static void OnConnect(object sender, EventArgs e)
        {
           
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.ConnectToServer);
            MyLog.Log("链接到服务器....客户端id为:"+_client.Id);
            //EventManager.DispatchEvent(MessageType.NetMessage, NetMessageType.ReLink, new object[] { _client.Id,_id });
            var msg=CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.ReLink);
            msg.AddUShort(_id);
            Send(msg);
            MyLog.Log("发送从连消息");
            
            _id = _client.Id;
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
            if (_client.IsConnected)_client.Send(message);
        }


        public static void DisConnect()
        {
            _client.Disconnect();
        }

        public static void CloseGame()
        {
            var msg=CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.CloseGame);
            Send(msg);
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
        
        private static void Instantiate(string packName,string spawnName,string typeName,Vector3 position,Vector3 rotation,bool isPlayer,bool isAb=true)
        {
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Instantiate);
            msg.AddString(packName);
            msg.AddString(spawnName);
            msg.AddString(typeName);
            msg.AddVector3(position);
            msg.AddVector3(rotation);
            msg.AddBool(isPlayer);
            msg.AddBool(isAb);
            Send(msg);
        }

        public static void Instantiate<T>(Vector3 position, Vector3 rotation, bool isPlayer, bool isAb = true)where T: Actor
        {
            var type = typeof(T);
            var actorInfo = type.GetCustomAttribute<ActorInfoAttribute>();
            if (actorInfo!=null)
            {
                Instantiate(actorInfo.PackName,actorInfo.PrefabName,type.Namespace+"."+type.Name,position,rotation,isPlayer,isAb);
            }
        }

        public static void Rpc<T>(string methodName,T actor,Rpc rpc,object[] param=null) where T: Actor
        {
            if (_client==null||!_client.IsConnected)return;
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Rpc);
            msg.AddString(methodName);
            msg.AddUShort(actor.GetIdentity().GetObjId());
            msg.AddUShort((ushort)rpc);
            msg.AddString(JsonConvert.SerializeObject(param));
            Send(msg);
        }
        
        // public static void Rpc<T>(string methodName,T netWorkSystemMono,Rpc rpc,object[] param=null) where T: NetWorkSystemMono
        // {
        //     if (_client==null||!_client.IsConnected)return;
        //     Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Rpc);
        //     msg.AddString(methodName);
        //     msg.AddUShort(netWorkSystemMono.GetId());
        //     msg.AddUShort((ushort)rpc);
        //     msg.AddString(JsonConvert.SerializeObject(param));
        //     Send(msg);
        // }
        
        
        
        public static void Destroy<T>(T actor) where T: Actor
        {
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Destroy);
            msg.AddUShort(actor.GetIdentity().GetObjId());
            Send(msg);
        }
        
        public static void Destroy(ushort id)
        {
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Destroy);
            msg.AddUShort(id);
            Send(msg);
        }

        public static void GetRoomInfo()
        {
            Message msg = CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.GetRoomInfo);
            Send(msg);
        }
        
        
        [MessageHandler((ushort)ServerToClientMessageType.SyncTick)]
        private static void SyncTick(Message message)
        {
            serverTick = message.GetUShort();
        }
    }
}
