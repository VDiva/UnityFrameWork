using GameData;
using NetWork;
using NetWork.Enum;
using NetWork.Tool;
using System.Net.Sockets;



namespace Server
{
    public class Server
    {
        static void Main(string[] args)
        {



            var serverUdp = new NetWorkSystem();
            serverUdp.OpenServer += OpenServer;
            var client1 = new NetWorkSystem().NetAsServerUdp("127.0.0.1", 8889,2048);
            client1.ReceiveSuccessAction += Receive;


            var client2 = new NetWorkSystem();
            client2.OpenServer += OpenServer;
            client2.ReceiveSuccessAction += Receive;
            client2.NetAsServerTcp("127.0.0.1", 7777, 100, 2048);

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
                Console.WriteLine("接受到消息大小"+ data.Length);
            }

        }
    }
}
