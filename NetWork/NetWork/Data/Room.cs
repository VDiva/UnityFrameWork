
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

        private Dictionary<ushort, Player> players;

        private ObjectPool<ObjDate> objectPoolGameObject;

        private Dictionary<ushort, ObjDate> gameObjects;

        private ushort objIndex=2000;
        
        public Room(int roomId,string roomName,int maxCount)
        {
            this.roomId = roomId;
            this.roomName = roomName;
            this.maxCount = maxCount;
            
            players = new Dictionary<ushort, Player>();
            messages = new List<Message>();
            gameObjects=new Dictionary<ushort, ObjDate>();
            objectPoolGameObject=new ObjectPool<ObjDate>();

        }

        public Room()
        {
            players = new Dictionary<ushort, Player>();
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
                    Player player = new Player(connection);
                    players.Add(id, player);

                    SendHistoryInformation(connection, () =>
                    {
                        Message msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.PlayerJoinRoom);
                        msg.AddUShort(id);
                        msg.AddInt(roomId);
                        msg.AddString(roomName);
                        SendOther(id,msg);

                        SendTmpTransfrom(id);

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


        public void PlayerDisConnect(ushort id)
        {
            if(players.ContainsKey(id))
            {
                players[id].IsDisConnect = true;
            
                foreach (var player in players)
                {
                    if (player.Value.Connection.Id != id)
                    {
                        SetBelongingClient(id, player.Value.Connection.Id);
                        break;
                    }
                }

                Task.Run((async () =>
                {
                    await Task.Delay(30000);
                    Left(id);
                    Console.WriteLine("玩家"+id+"断开了链接");
                }));
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
                        if (player.Value.Connection.Id!=id)
                        {
                            SetBelongingClient(id, player.Value.Connection.Id);
                            break;
                        }
                    }
                }
            }
        }


        public void Transform(ushort id,Message message,ushort objId,Vector3 pos,Vector3 rot)
        {
           SendOther(id,message,false);
           if(gameObjects.TryGetValue(objId,out var objDate))
           {
                objDate.Position = pos;
                objDate.Rotation = rot;
           }
        }

        public void Instantiate(ushort id,Message message)
        {
            Message msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.Instantiate);
            
            var go=objectPoolGameObject.DeQueue();

            go.BelongingClient = id;
            go.PackName = message.GetString();
            go.SpawnName = message.GetString();
            go.TypeName = message.GetString();
            go.Position = message.GetVector3();
            go.Rotation = message.GetVector3();

            
            
            bool isPlayer=message.GetBool();
            var isAb = message.GetBool();
            msg.AddUShort(id);
            ushort objId;
            if (isPlayer)
            {
                objId = id;
            }
            else
            {
                objId = objIndex;
                objIndex += 1;
            }


            msg.AddUShort(objId);
            msg.AddGameObject(go);

            msg.AddBool(isAb);
            gameObjects.TryAdd(objId, go);

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
                if(go.Key!=go.Value.BelongingClient&&go.Value.BelongingClient == id)
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

        private void SendTmpTransfrom(ushort id)
        {
            foreach(var item in gameObjects)
            {
                if (item.Value.BelongingClient!=id)
                {
                    var msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.Transform);
                    msg.AddUShort(item.Key);
                    msg.AddVector3(item.Value.Position);
                    msg.AddVector3(item.Value.Rotation);
                    SendSelf(id,msg);
                }
            }
        }

        public void ReLink(ushort newId,ushort oldId)
        {
            if(players.ContainsKey(oldId))
            {
                var player = players[oldId];
                player.IsDisConnect = false;
                player.Connection = NetWorkSystem.GetClient(newId);
                players.Remove(oldId);
                players.Add(newId, player);
                
                
                 if (gameObjects.ContainsKey(oldId))
                 {
                     var obj = gameObjects[oldId];
                     obj.objId = newId;
                     obj.BelongingClient = newId;
                     gameObjects.Remove(oldId);
                     gameObjects.Add(newId,obj);
                     
                 }
                
                
                
                 var msg = NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.ReLink);
                 msg.AddUShort(newId);
                 msg.AddUShort(oldId);
                 SendAll(msg);

                

                  for (var i = 0;i < player.Messages.Count;i++)
                  {
                      SendSelf(newId, player.Messages[i]);
                  }
                
                  SendTmpTransfrom(newId);
            }
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
                if (!player.Value.IsDisConnect)
                {
                    Console.WriteLine("发送消息all");
                    player.Value.Connection.Send(message, !isAdd);
                }
                else
                {
                    if(isAdd) player.Value.Messages.Add(message);
                }
            }
        }

        public void SendOther(ushort id,Message message, bool isAdd = true)
        {
            if (isAdd) AddMessage(message);
            foreach (var player in players)
            {
                if (player.Value.Connection.Id != id)
                {
                    if (!player.Value.IsDisConnect)
                    {
                        Console.WriteLine("发送消息other");
                        player.Value.Connection.Send(message, !isAdd);
                    }
                    else
                    {
                        if (isAdd) player.Value.Messages.Add(message);
                    }
                }
            }
        }

        public void SendSelf(ushort id, Message message, bool isAdd = true)
        {
            if (isAdd) AddMessage(message);

            if (players.ContainsKey(id))
            {
                var player = players[id];
                if (player.Connection.Id == id)
                {
                    if (!player.IsDisConnect)
                    {
                        player.Connection.Send(message, !isAdd);
                        Console.WriteLine("发送消息self");
                    }
                    else
                    {
                        if (isAdd) player.Messages.Add(message);
                    }
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
