﻿
using NetWork.Data;
using NetWork.Tool;
using NetWork.Type;
using Newtonsoft.Json;
using Riptide;
using System.Reflection;

namespace NetWork
{
    public class Room
    {
        public int roomId;
        public string roomName;

        public int maxCount;
        //private List<Connection> players;
        private List<Message> messages;

        private Dictionary<ushort, Connection> players;

        private ObjectPool<ObjDate> objectPoolGameObject;

        private Dictionary<ushort, ObjDate> gameObjects;

        private ushort objIndex=2000;
        
        public Room(int roomId,string roomName,int maxCount)
        {
            this.roomId = roomId;
            this.roomName = roomName;
            this.maxCount = maxCount;
            
            players = new Dictionary<ushort, Connection>();
            messages = new List<Message>();
            gameObjects=new Dictionary<ushort, ObjDate>();
            objectPoolGameObject=new ObjectPool<ObjDate>();

        }

        public Room()
        {
            players = new Dictionary<ushort, Connection>();
            messages = new List<Message>();
            gameObjects = new Dictionary<ushort, ObjDate>();
            objectPoolGameObject = new ObjectPool<ObjDate>();
        }

        public void Init(int roomId, string roomName, int maxCount)
        {
            this.roomId = roomId;
            this.roomName = "房间"+roomId+"-"+roomName;
            this.maxCount = maxCount;

            ReleaseMessage();
            objIndex = 2000;   
            messages.Clear();
            players.Clear();
            gameObjects.Clear();

        }
        public void Init( string roomName, int maxCount)
        {
            this.roomName = "房间" + roomId + "-" + roomName;
            this.maxCount = maxCount;
            ReleaseMessage();
            objIndex = 2000;
            messages.Clear();
            players.Clear();
            gameObjects.Clear();
        }

        public bool Join(ushort id,Connection connection)
        {
            if (!players.ContainsKey(id))
            {
                if (players.Count < maxCount)
                {
                    Console.WriteLine(id + ":加入了房间:" + roomId);
                    players.Add(id, connection);

                    SendHistoryInformation(connection, () =>
                    {
                        Message msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.PlayerJoinRoom);
                        msg.AddUShort(id);
                        msg.AddInt(roomId);
                        msg.AddString(roomName);
                        SendAll(msg);
                    });
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }


        public void Left(ushort id)
        {
            if (players.ContainsKey(id))
            {
                Message msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.PlayerLeftRoom);
                msg.AddUShort(id);
                SendAll(msg);
                gameObjects.Remove(id);
                players.Remove(id);



                Console.WriteLine(id + ":离开了房间:" + roomId);
                if(players.Count == 0)
                {
                    Console.WriteLine(roomId+"房间空 回收房间");
                    RoomSystem.EnQueue(this);
                }
                else
                {
                    foreach (var player in players)
                    {
                        SetBelongingClient(id, player.Value.Id);
                        break;
                    }
                }
            }
        }


        public void Transform(Message message)
        {
           SendAll(message,false);
        }


        public void Instantiate(ushort id,Message message)
        {
            Message msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.Instantiate);
            
            var go=objectPoolGameObject.DeQueue();

            go.BelongingClient = id;
            go.SpawnName = message.GetString();
            go.Position = message.GetVector3();
            go.Rotation = message.GetVector3();

            
            
            bool isPlayer=message.GetBool();
            var isAb = message.GetBool();
            if (isPlayer)
            {
                msg.AddUShort(id);
                msg.AddUShort(id);
            }
            else
            {
                msg.AddUShort(id);
                msg.AddUShort(objIndex);
                objIndex += 1;
            }



            msg.AddGameObject(go);

            msg.AddBool(isAb);
            gameObjects.TryAdd(objIndex, go);

            SendAll(msg);

           
        }

        public void Rpc(ushort id, Message message)
        {
            Message msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ClientToServerMessageType.Rpc);

            var methodName = message.GetString();
            var objId = message.GetUShort();
            var rpc = message.GetUShort();
            var param = message.GetString();

            msg.AddString(methodName);
            msg.AddUShort(objId);
            msg.AddString(param);
            if (rpc == 0)
            {
                SendAll(msg);
            }
            else
            {
                SendOther(id, msg);
            }
        }

        public void SetBelongingClient(ushort id,ushort newId)
        {
            List<ushort> ids = new List<ushort>();

            foreach(var go in gameObjects)
            {
                if(go.Value.BelongingClient == id)
                {
                    ids.Add(go.Key);
                    go.Value.BelongingClient = newId;
                }
            }

            Message msg=NetWorkSystem.CreateMessage(MessageSendMode.Reliable,ServerToClientMessageType.SetBelongingClient);
            msg.AddUShort(newId);
            msg.AddInt(ids.Count);
            for(var i = 0; i < ids.Count; i++)
            {
                msg.AddUShort(ids[i]);
            }

            SendAll(msg);
        }


        public void GetId(ushort id, Message message)
        {
            
        }


        private void SendHistoryInformation(Connection connection,Action action=null)
        {
            
            foreach (Message message in messages)
            {
                connection.Send(message,false);
            }

            action?.Invoke();
        }


        public void Destroy(ushort id)
        {
            gameObjects.Remove(id, out var go);

            Message msg=NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.Destroy);
            msg.AddUShort(id);
            SendAll(msg);

        }

        public void SendAll(Message message,bool isAdd=true)
        {
            if(isAdd) AddMessage(message);
            foreach (var player in players)
            {
                player.Value.Send(message,false);
            }
        }

        public void SendOther(ushort id,Message message, bool isAdd = true)
        {
            if (isAdd) AddMessage(message);
            foreach (var player in players)
            {
                if (player.Value.Id != id)
                {
                    player.Value.Send(message, false);
                }
            }
        }

        public void SendSelf(ushort id, Message message, bool isAdd = true)
        {
            if (isAdd) AddMessage(message);
            foreach (var player in players)
            {
                if (player.Value.Id == id)
                {
                    player.Value.Send(message, false);
                }
            }
            
        }

        private void AddMessage(Message message)
        {
            messages.Add(message);
        }


        private void ReleaseMessage()
        {
            for(var i=0; i<messages.Count; i++)
            {
                messages[i].Release();
            }
        }

    }
}