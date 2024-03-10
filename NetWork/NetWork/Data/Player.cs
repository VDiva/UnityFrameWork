using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetWork.Data
{
    public class Player
    {
        public Connection Connection;
        public List<Message> Messages;
        public bool IsDisConnect;

        public Player(Connection connection) 
        {
            Connection = connection;
            Messages = new List<Message>();
            IsDisConnect = false;
            
        }

    }
}
