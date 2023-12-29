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
            
            NetWorkSystem.OpenServer += OpenServer;
            NetWorkSystem.acceptAction += Accpet;

            var client = NetWorkSystem.NetAsServer("127.0.0.1", 8889, 100, 2048, ConnectType.Udp);

            client.ReceiveSuccessAction += Receive;

            
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
                Console.WriteLine("接受到消息大小", data.Length);
            }

        }
    }
}
