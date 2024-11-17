using NetWork;

public class ClientMain
{
    static void Main(string[] args)
    {
        NetWork.NetWork.InitLog((log =>Console.WriteLine(log)));
        NetWorkAsClient.Connect("127.0.0.1",8888);
        Task.Run((() =>
        {
            Task.Delay(50).Wait();
            NetWorkAsClient.Update();
            
            //NetWorkAsClient.Send();
        }));
        
        
        
        Task.Run((() =>
        {
            while (true)
            {
                var key = Console.ReadLine();
                switch (key)
                {
                    case "a":
                        NetWorkAsClient.CreateRoom("大傻逼",5);
                        break;
                    case "s":
                        NetWorkAsClient.LeaveRoom();
                        break;
                }
            }
        }));

        while (true)
        {
            
        }
        //NetWorkAsClient.Start(8888, 100);

    }
}

