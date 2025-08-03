using Library.EventSystem;
using Library.Msg;
using Riptide;

namespace NetWorkServer.Mrg
{
    public static class MsgSystem
    {
        
        [MessageHandler((ushort)RoomType.CreateRoom)]
        public static void CreateRoom(ushort id, Message msg)
        {
            var connection = NetServer.GetConnection(id);
            var roomName = msg.GetString();
            var maxPlayer = msg.GetUShort();
            RoomMrg.CreateRoom(connection,roomName ,maxPlayer);
        }
        
        [MessageHandler((ushort)RoomType.MatchingRoom)]
        public static void MatchingRoom(ushort id, Message msg)
        {
            var connection = NetServer.GetConnection(id);
            RoomMrg.MatchingRoom(connection);
        }
        
        
        [MessageHandler((ushort)RoomType.JoinRoom)]
        public static void JoinRoom(ushort id, Message msg)
        {
            var roomId = msg.GetUShort();
            var connection = NetServer.GetConnection(id);
            if (connection!=null)
            {
                var eventMsg = EventSystem.GetEventMsg();
                eventMsg.Add(roomId);
                eventMsg.Add(connection);
                EventSystem.DispatchEvent(MsgType.Room,RoomType.JoinRoom);
            }
        }
        
        
        [MessageHandler((ushort)RoomType.LeaveRoom)]
        public static void LeaveRoom(ushort id, Message msg)
        {
            var connection = NetServer.GetConnection(id);
            if (connection!=null)
            {
                var eventMsg = EventSystem.GetEventMsg();
                eventMsg.Add(connection);
                EventSystem.DispatchEvent(MsgType.Room,RoomType.LeaveRoom,eventMsg);
            }
        }
        
        [MessageHandler((ushort)RoomType.Retransmission)]
        public static void Retransmission(ushort id, Message msg)
        {
            var roomId = msg.GetUShort();
            var eventMsg = EventSystem.GetEventMsg();
            eventMsg.Add(roomId);
            eventMsg.Add(msg);
            EventSystem.DispatchEvent(MsgType.Room,RoomType.Retransmission,eventMsg);
        }
        
        
        [MessageHandler((ushort)RoomType.InputData)]
        public static void InputData(ushort id, Message msg)
        {
            var roomId = msg.GetUShort();
            var eventMsg = EventSystem.GetEventMsg();
            eventMsg.Add(roomId);
            eventMsg.Add(msg);
            EventSystem.DispatchEvent(MsgType.Room,RoomType.InputData,eventMsg);
        }
    }
}