using Riptide;

namespace TestClient;

public class Test
{
    static void Main()
    {
        var client=NetWork.SatrtClient("127.0.0.1",8888,true);
        Console.WriteLine("----------------------------------");
        var msg = Message.Create(MessageSendMode.Reliable,1);
        msg.AddString("你好");
        client.Send(msg);
        Console.ReadKey();
    }
}