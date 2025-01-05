using NetWorkServer;

namespace TestServer;

public class TestServer
{
    public static void Main(string[] args)
    {
        NetServer.Start(8888,100);
        Console.ReadKey();
    }
}