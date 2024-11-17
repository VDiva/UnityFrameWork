using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrameWork;
using Riptide;

namespace FrameWork
{
    public static class RoomMrg
    {
        private static Dictionary<ushort,Room> _rooms = new Dictionary<ushort, Room>();
        private static Dictionary<ushort, Room> _playerIdByRooms = new Dictionary<ushort, Room>();
        private static ObjectPool<Room> _roomPool = new ObjectPool<Room>();
        private static ushort _index=0;
        
        static RoomMrg()
        {
            Console.WriteLine("房间管理器初始化成功!!!!!");
            NetWorkAsServer.OnUpdate += Update;
        }
        
        private static void Update()
        {
            Task.Delay(50).Wait();
            var keys = _rooms.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                _rooms[keys[i]].Update();
            }
        }
        
        [MessageHandler((ushort)NetMsgType.JoinRoom)]
        public static void JoinRoom(ushort clientId,Message message)
        {
            //var playerId = message.GetUShort();
            var roomId = message.GetUShort();
            if (_rooms.ContainsKey(roomId))
            {
                _rooms[roomId].JoinRoom(clientId);
                _playerIdByRooms.Add(clientId, _rooms[roomId]);
                Console.WriteLine($"玩家ID：{clientId} 加入了 房间ID:{roomId}");
            }
            else
            {
                var msg = NetWork.GetMsg(MessageSendMode.Reliable, NetMsgType.JoinRoomFailed);
                NetWorkAsServer.SendToClient(clientId,msg);
                Console.WriteLine($"玩家ID：{clientId} 加入 房间ID:{roomId} 失败 因为房间不存在");
            }
            
            message.Release();
        }
        
        
        [MessageHandler((ushort)NetMsgType.CreateRoom)]
        public static void CreateRoom(ushort clientId,Message message)
        {
            var roomName = message.GetString();
            var roomMaxPlayer = message.GetUShort();
            var roomPassword = message.GetString();
            var room=_roomPool.DeQueue();
            room.InitRoom(_index, roomName, roomPassword,roomMaxPlayer);
            room.JoinRoom(clientId);
            _playerIdByRooms.Add(clientId,room);
            _rooms.TryAdd(room.GetRoomId(),room);
            _index += 1;
            message.Release();
            Console.WriteLine($"玩家ID：{clientId} 创建了房间ID:{room.GetRoomId()} 房间名:{roomName}");
        }
        
        [MessageHandler((ushort)NetMsgType.LeaveRoom)]
        public static void LeaveRoom(ushort clientId,Message message)
        {
            if (_playerIdByRooms.ContainsKey(clientId))
            {
                
                Console.WriteLine($"玩家ID：{clientId} 离开了房间ID:{_playerIdByRooms[clientId].GetRoomId()}");
                _playerIdByRooms[clientId].LeaveRoom(clientId);
                _playerIdByRooms.Remove(clientId);
            }
            
            message.Release();
        }
        
        
        [MessageHandler((ushort)NetMsgType.Msg)]
        public static void Msg(ushort clientId,Message message)
        {
            var roomId = message.GetUShort();
            if (_rooms.ContainsKey(roomId))
            {
                _rooms[roomId].Msg(message);
            }
        }


        public static void Enqueue(Room room)
        {
            if (_rooms.ContainsKey(room.GetRoomId()))
            {
                _rooms.Remove(room.GetRoomId());
                _roomPool.EnQueue(room);
                Console.WriteLine($"房间ID：{room.GetRoomId()} 玩家为空 以被回收!!!");
            }
        }
    }
}