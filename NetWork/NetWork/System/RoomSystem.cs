using NetWork.System;
using NetWork.Type;
using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetWork
{
    public static class RoomSystem
    {

        private static Dictionary<int, Room> rooms;
        private static Dictionary<ushort, Room> playerIdGetRoom;
        private static int index;

        static RoomSystem()
        {
            rooms = new Dictionary<int, Room>();
            playerIdGetRoom = new Dictionary<ushort, Room>();
        }

        [MessageHandler((ushort)ClientToServerMessageType.JoinRoom)]
        private static void JoinRoom(ushort id,Message message)
        {
            var roomId = message.GetInt();
            if (rooms.ContainsKey(roomId))
            {
                if (!rooms[roomId].Join(id,NetWorkSystem.GetClient(id)))
                {
                    SendError(id, "房间以满");
                }
            }
            else
            {
                SendError(id, "房间不存在");
            }
        }

        [MessageHandler((ushort)ClientToServerMessageType.CreateRoom)]
        private static void CreateRoom(ushort id, Message message)
        {
            var roomName = message.GetString();
            var roomCount = message.GetInt();
            Room room = new Room(index, roomName, roomCount);
            rooms.Add(index, room);
            playerIdGetRoom.Add(id,room);
            index += 1;
        }

        [MessageHandler((ushort)ClientToServerMessageType.MatchingRoom)]
        private static void MatchingRoom(ushort id, Message message)
        {
            var client = NetWorkSystem.GetClient(id);
            if (rooms.Count>0)
            {

                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].Join(id,client))
                    {
                        return;
                    }
                }

                Room room = new Room(index, message.GetString(), message.GetInt());
                rooms.Add(index, room);
                playerIdGetRoom.Add(id, room);
                index += 1;
            }
            else
            {
                Room room = new Room(index, message.GetString(), message.GetInt());
                rooms.Add(index, room);
                playerIdGetRoom.Add(id, room);
                index += 1;
            }
        }



        [MessageHandler((ushort)ClientToServerMessageType.TransfromAll)]
        private static void TransfromAll(ushort id,Message message)
        {
            if(playerIdGetRoom.TryGetValue(id, out var room))
            {
                room.TransfromAll(message);
            }
        }


        [MessageHandler((ushort)ClientToServerMessageType.TransfromOther)]
        private static void TransfromOther(ushort id, Message message)
        {
            if (playerIdGetRoom.TryGetValue(id, out var room))
            {
                room.TransfromOther(id,message);
            }
        }


        private static void SendError(ushort id,string info)
        {
            var message = NetWorkSystem.CreateMessage(MessageSendMode.Unreliable, ServerToClientMessageType.JoinError);
            message.AddString(info);
            NetWorkSystem.Send(message,id);
        }
    }
}
