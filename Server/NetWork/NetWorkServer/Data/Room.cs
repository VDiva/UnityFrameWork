using System;
using System.Collections.Generic;
using Library.EventSystem;
using Library.Msg;
using Riptide;

namespace NetWorkServer.Data
{
    public class Room
    {
        private string _roomName;//房间名字
        private ushort _roomID; //房间id
        private ushort _curPlayerCount;//房间当前玩家数量
        private ushort _roomMaxPlayers; //房间最大玩家数量
        private List<ushort> _playerList=new List<ushort>(); //房间玩家id
        private bool _isOpen; //是否正在使用状态

        public Room()
        {
            EventSystem.AddListener(MsgType.Room,RoomType.JoinRoom,JoinRoom);
            EventSystem.AddListener(MsgType.Room,RoomType.LeaveRoom,LeaveRoom);
            EventSystem.AddListener(MsgType.Room,RoomType.Retransmission,Retransmission);
        }
        
        public void Init(string roomName, ushort roomID, ushort roomMaxPlayers)
        {
            _playerList.Clear();
            _isOpen = true;
            _roomName=roomName;
            _roomID=roomID;
            _roomMaxPlayers=roomMaxPlayers;
        }


        public void SetOpen(bool isOpen)
        {
            _isOpen=isOpen;
        }
        

        public void Init(string roomName, ushort roomMaxPlayers)
        {
            _playerList.Clear();
            _isOpen = true;
            _roomName=roomName;
            _roomMaxPlayers=roomMaxPlayers;
        }

        public ushort GetRoomId()
        {
            return _roomID;
        }
        
        public string GetRoomName()
        {
            return _roomName;
        }

        public bool IsCanJoinRoom()
        {
            return _curPlayerCount < _roomMaxPlayers;
        }

        //转发
        private void Retransmission(List<object> objects)
        {
            if (!_isOpen)return;
            var roomId=(ushort)objects[0];
            if (roomId!=_roomID)return;
            var msg = (Message)objects[1];
            var m = MsgMrg.CreateMsg(msg.SendMode, RoomType.Retransmission);
            m.AddMessage(msg);
            NetServer.Send(m, _playerList.ToArray());
        }
        
        
        
        private void JoinRoom(List<object> objects)
        {
            if (!_isOpen)return;
            var roomId=(ushort)objects[0];
            if (roomId!=_roomID)return;
            var connection = (Connection)objects[1];
            _playerList.Add(connection.Id);
            Console.WriteLine(connection.Id+":加入房间"+_roomName+_roomID);
            
            var msg = MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.JoinRoom);
            msg.AddUShort(_roomID);
            msg.AddUShort(connection.Id);
            NetServer.Send(msg, _playerList.ToArray());
            _curPlayerCount += 1;
        }
        
        
        private void LeaveRoom(List<object> objects)
        {
            if (!_isOpen)return;
            
            var connection = (Connection)objects[0];

            if (!_playerList.Contains(connection.Id))return;
            
            Console.WriteLine(connection.Id+":离开房间"+_roomName+_roomID);
            _playerList.Remove(connection.Id);
            var msg = MsgMrg.CreateMsg(MessageSendMode.Reliable, RoomType.LeaveRoom);
            msg.AddUShort(connection.Id);
            NetServer.Send(msg, _playerList.ToArray());
            _curPlayerCount -= 1;

            if (_curPlayerCount==0)
            {
                var eventMsg = EventSystem.GetEventMsg();
                eventMsg.Add(this);
                EventSystem.DispatchEvent(MsgType.Room,RoomType.RoomRecycle,eventMsg);
            }
            
            
        }
        
        
    }
}