using System;
using System.Threading.Tasks;
using Riptide;
using Riptide.Utils;

public static class NetWork
{
    private static Server _server;
    private static Client _client;
    public static void SatrtServer(int port, int maxConnect)
    {
        _server = new Server();
        _server.ClientConnected += ClientConnected;
        _server.ClientDisconnected += ClientDisconnected;
        _server.ConnectionFailed += ConnectionFailed;
        _server.MessageReceived += MessageReceived;
        _server.Start(8888,100);
        RiptideLogger.Initialize(Console.WriteLine,true);
        Task.Run(UpdateServer);
    }

    private static void ClientConnected(object obj,ServerConnectedEventArgs connectedEventArgs)
    {
        Console.WriteLine($"客户端ID:{connectedEventArgs.Client.Id}---链接到服务器");
    }
    
    private static void ClientDisconnected(object obj,ServerDisconnectedEventArgs serverDisconnectedEvent)
    {
        Console.WriteLine($"客户端ID:{serverDisconnectedEvent.Client.Id}---断开服务器");
    }
    
    private static void ConnectionFailed(object obj,ServerConnectionFailedEventArgs serverConnectionFailed)
    {
        Console.WriteLine($"服务器开启失败");
    }
    
    private static void MessageReceived(object obj,MessageReceivedEventArgs messageReceivedEvent)
    {
        Console.WriteLine($"接受到消息{messageReceivedEvent.Message.GetString()}");
    }
    

    private static void UpdateServer()
    {
        while (true)
        {
            Task.Delay(50);
            _server.Update();
        }
    }
    
    
    public static Client SatrtClient(string ip, int port,bool isUpdate=false)
    {
        _client = new Client();
        _client.Connected += Connected;
        _client.ConnectionFailed += ConnectionFailed;
        _client.Connect(ip+":"+port);
        if (isUpdate)
        {
            Task.Run(UpdateClient);
            RiptideLogger.Initialize(Console.WriteLine,true);
        }
        return _client;
    }

    private static void Connected(object obj, EventArgs eventArgs)
    {
        Console.WriteLine("链接到服务器");
    }
    
    private static void ConnectionFailed(object obj, EventArgs eventArgs)
    {
        Console.WriteLine("已断开服务器");
    }
    
    private static void UpdateClient()
    {
        while (true)
        {
            Task.Delay(50);
            _client.Update();
        }
    }
    
}