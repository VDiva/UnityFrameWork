using System;
using System.Threading.Tasks;
using Riptide;
using Riptide.Utils;

public static class NetWork
{
    private static Server _server;
    private static Client _client;

    public static Action<object, ServerConnectedEventArgs> OnClientConnected;
    public static Action<object, ServerDisconnectedEventArgs> OnClientDisconnected;
    public static Action<object, ServerConnectionFailedEventArgs> OnConnectionFailed;
    public static Action<object, MessageReceivedEventArgs> OnMessageReceived;
    
    public static Action<object, EventArgs> OnConnected;
    public static Action<object, EventArgs> OnDisconnected;
    
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
        OnClientConnected?.Invoke(obj,connectedEventArgs);
    }
    
    private static void ClientDisconnected(object obj,ServerDisconnectedEventArgs serverDisconnectedEvent)
    {
        OnClientDisconnected?.Invoke(obj,serverDisconnectedEvent);
    }
    
    private static void ConnectionFailed(object obj,ServerConnectionFailedEventArgs serverConnectionFailed)
    {
        OnConnectionFailed?.Invoke(obj,serverConnectionFailed);
    }
    
    private static void MessageReceived(object obj,MessageReceivedEventArgs messageReceivedEvent)
    {
        OnMessageReceived?.Invoke(obj,messageReceivedEvent);
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
        _client.Disconnected += Disconnected;
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
        OnConnected?.Invoke(obj,eventArgs);
    }
    
    private static void Disconnected(object obj, EventArgs eventArgs)
    {
        OnDisconnected?.Invoke(obj,eventArgs);
    }
    
    private static void UpdateClient()
    {
        while (true)
        {
            Task.Delay(50);
            _client.Update();
        }
    }


    public static void Send(Message message,ushort id,bool shouldRelease=true)
    {
        if (_server.IsRunning)
        {
            _server.Send(message, id, shouldRelease);
        }
    }
    
    public static void Send(Message message,ushort[] ids,bool shouldRelease=true)
    {
        if (_server.IsRunning)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                _server.Send(message, ids[i], shouldRelease);
            }
        }
    }

    public static void SendAll(Message message, bool shouldRelease = true)
    {
        _server.SendToAll(message,shouldRelease);
    }

    public static void Send(Message message)
    {
        if (_client.IsConnected)
        {
            _client.Send(message);
        }
    }

    public static Message CreateMsg(MessageSendMode sendMode)
    {
        return Message.Create(sendMode);
    }


    public static bool TryGetClient(ushort id,out Connection connection)
    {
        return _server.TryGetClient(id, out connection);
    }
    
}