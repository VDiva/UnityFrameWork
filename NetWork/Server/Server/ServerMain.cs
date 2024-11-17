using FrameWork;


namespace Server;

public class ServerMain
{
    static void Main(string[] args)
    {
        
        NetWork.InitLog((log =>Console.WriteLine(log)));
        NetWorkAsServer.Start(8888, 100);
        Console.ReadKey();
    }
}