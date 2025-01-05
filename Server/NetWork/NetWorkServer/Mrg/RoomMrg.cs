using System;
using System.Collections.Generic;
using Library.EventSystem;
using Library.Msg;
using Library.ObjectPool;
using NetWorkServer.Data;
using Riptide;

namespace NetWorkServer.Mrg
{
    public static class RoomMrg
    {
        private static ObjectPool<Room> roomPool=new ObjectPool<Room>();
        private static Dictionary<ushort, Room> roomDic = new Dictionary<ushort, Room>();
        private static ushort _index=1;

        static RoomMrg()
        {
            EventSystem.AddListener(MsgType.Room,RoomType.RoomRecycle,RoomRecycle);
            NetServer.ClientDisconnected += DisConnect;
            Console.WriteLine("初始化房间管理器");
        }

        public static void Init()
        {
            
        }

        private static void DisConnect(object sender, ServerDisconnectedEventArgs e)
        {
            var eventMsg = EventSystem.GetEventMsg();
            eventMsg.Add(e.Client);
            EventSystem.DispatchEvent(MsgType.Room,RoomType.LeaveRoom,eventMsg);
        }
        
        public static Room CreateRoom(Connection connection,string roomName, ushort maxPlayers)
        {
            
            var room = roomPool.DeQueue((room1 =>
            {
                room1.Init(roomName, _index, maxPlayers);
                _index += 1;
            } ));
            room.Init(roomName,maxPlayers);
            Console.WriteLine("创建房间"+roomName+room.GetRoomId());
            var eventMsg = EventSystem.GetEventMsg();
            eventMsg.Add(room.GetRoomId());
            eventMsg.Add(connection);
            EventSystem.DispatchEvent(MsgType.Room,RoomType.JoinRoom,eventMsg);
            return room;
        }

        public static Room MatchingRoom(Connection connection)
        {
            foreach (var room in roomDic)
            {
                if (room.Value.IsCanJoinRoom())
                {
                    var eventMsg = EventSystem.GetEventMsg();
                    eventMsg.Add(room.Key);
                    eventMsg.Add(connection);
                    EventSystem.DispatchEvent(MsgType.Room,RoomType.JoinRoom,eventMsg);
                    return room.Value;
                }
            }

            return CreateRoom(connection, "房间" + _index, 4);
        }

        private static void RoomRecycle(List<object> objects)
        {
            var room = (Room)objects[0];
            Console.WriteLine("房间:"+room.GetRoomName()+room.GetRoomId()+"回收");
            room.SetOpen(false);
            roomDic.Remove(room.GetRoomId());
            roomPool.EnQueue(room);
        }
    }
}