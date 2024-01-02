using GameData;
using NetWork;
using NetWork.Enum;
using NetWork.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ClientTest
{
    public class ClientTest
    {
        static void Main(string[] args)
        {
            var client1 =new NetWorkSystem().NetAsClientTcp("127.0.0.1", 7777, 2048);
            var client2 =new NetWorkSystem().NetAsClientUdp("127.0.0.1", 8888, 2048);
            client2.Rpc(MessageSendType.All, "");
            EndPoint point = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8889);
            while (true)
            {
                Thread.Sleep(1000);
                client1.SendMessage(new Data());
                client2.SendMessage(point, new Data());
            }
        }


        static void Receive(byte[] data, object obj, SocketAsyncEventArgs args)
        {

            if (Tool.DeSerialize(data, out Data da))
            {
                Console.WriteLine("接受到消息大小", data.Length);
            }

        }
    }
}
