using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetWork.System;
using NetWork.Type;
using Riptide;

namespace NetWork
{
    public class Room
    {
        private int roomId;
        private string roomName;

        private int maxCount;
        //private List<Connection> players;
        private List<Message> messages;

        private Dictionary<int, Connection> players;

        public Room(int roomId,string roomName,int maxCount)
        {
            this.roomId = roomId;
            this.roomName = roomName;
            this.maxCount = maxCount;

            players = new Dictionary<int, Connection>();
            messages = new List<Message>();
           
        }

        public bool Join(ushort id,Connection connection)
        {

            if(players.Count < maxCount)
            {
                players.Add(id, connection);
                foreach (Message message in messages)
                {
                    connection.Send(message);
                }
                Message msg=NetWorkSystem.CreateMessage(MessageSendMode.Reliable, ServerToClientMessageType.PlayerJoinRoom);
                msg.AddUShort(id);
                SendAll(msg);
                return true;
            }
            else
            {
                return false;
            }
        }



        public void TransfromAll(Message message)
        {
            SendAll(message);
        }


        public void TransfromOther(ushort id, Message message)
        {
            SendOther(id, message);
        }


        public void SendAll(Message message)
        {
            for(int i = 0; i < players.Count; i++)
            {
                players[i].Send(message);
            }

            messages.Add(message);
        }

        public void SendOther(ushort id,Message message)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var client = players[i];
                if (client.Id != id)
                {
                    client.Send(message);
                }
            }
            messages.Add(message);
        }


    }
}
