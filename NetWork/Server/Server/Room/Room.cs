using System.Collections.Generic;
using System.Linq;
using Riptide;

namespace FrameWork
{
    public class Room
    {
        private List<ushort> _players=new List<ushort>();
        private string _roomPassword;
        private string _roomName;
        private ushort _roomId;
        private ushort _roomMaxPlayers;
        public void InitRoom(ushort roomId,string roomName,string roomPassword,ushort roomMaxPlayers)
        {
            _players.Clear();
            _roomId = roomId;
            _roomName=roomName;
            _roomPassword=roomPassword;
            _roomMaxPlayers = roomMaxPlayers;
        }

        public bool JoinRoom(ushort playerId)
        {
            if (_players.Count >= _roomMaxPlayers)
            {
                return false;
            }
            var msg=NetWork.GetMsg(MessageSendMode.Reliable,NetMsgType.JoinRoom);
            msg.AddUShort(playerId);
            SendMsgToAll(msg);
            _players.Add(playerId);
            return true;
        }

        public void LeaveRoom(ushort playerId)
        {
            var msg=NetWork.GetMsg(MessageSendMode.Reliable,NetMsgType.LeaveRoom);
            msg.AddUShort(playerId);
            SendMsgToAll(msg);
            _players.Remove(playerId);
            if (_players.Count==0)
            {
                RoomMrg.Enqueue(this);
            }
        }
        
        public void Update()
        {
            
        }


        public ushort GetRoomId()
        {
            return _roomId; 
        }
        
        public void Msg(Message message)
        {
            SendMsgToAll(message);
            message.Release();
        }
        
        private void SendMsgToOther(ushort playerId, Message msg)
        {
            NetWorkAsServer.SendToClient(_players.Where((arg => !arg.Equals(playerId))).ToArray(),msg);
        }
        
        private void SendMsg(ushort playerId, Message msg)
        {
            NetWorkAsServer.SendToClient(playerId,msg);
        }
        
        private void SendMsgToAll(Message msg)
        {
            NetWorkAsServer.SendToClient(_players.ToArray(),msg);
        }
    }
}