using GameData;
using NetWork;
using NetWork.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace Server
{
    public class Server
    {
        static void Main(string[] args)
        {

            NetWorkSystem.OpenServer += OpenServer;
            NetWorkSystem.acceptAction += Accpet;
            NetWorkSystem.ReceiveSuccessAction += Receive;

            NetWorkSystem.NetAsServer("127.0.0.1", 8888, 100, 2048);
            Console.ReadKey();
        }

        static void OpenServer()
        {
            Console.WriteLine("服务器已开启");
        }

        static void Accpet(object obj,SocketAsyncEventArgs args)
        {
            Console.WriteLine(args.RemoteEndPoint + ":链接到服务器");
        }

        static void Receive(byte[] data,object obj,SocketAsyncEventArgs args) 
        {

            if (Tool.DeSerialize(data, out Data da))
            {
                Vector3Data vec3 = da.TransfromData.PositionData.Vector3Data;
                Console.WriteLine("x:"+vec3.X+"-y:"+vec3.Y+"-z:"+vec3.Z);
            }

        }
    }
}
