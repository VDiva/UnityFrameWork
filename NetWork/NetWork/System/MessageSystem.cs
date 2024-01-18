using NetWork.Type;
using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetWork.System
{
    public class MessageSystem
    {
        [MessageHandler((ushort)ServerToClientMessageType.Login)]
        public static void Login(ushort id,Message message)
        {
            Console.WriteLine(message.GetString());
        }
    }
}
