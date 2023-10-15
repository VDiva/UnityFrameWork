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
           NetWorkSystem netWork=new NetWorkSystem();
            netWork.OpenServer += OpenServer;
            netWork.acceptAction += Accpet;
            netWork.ReceiveSuccessAction += Receive;
            var dataByte=Tool.Serialize(new GameData.Data() { Name="adwad"});
            var data = Tool.DeSerialize(dataByte, out GameData.Data da);
            Console.WriteLine(da.Name);

            netWork.NetAsServer("127.0.0.1", 8888, 100, 2048);
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
                Console.WriteLine(da.Name);
            }

        }
    }
}
