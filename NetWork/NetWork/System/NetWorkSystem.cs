
using Riptide;
using Riptide.Utils;

namespace NetWork.System
{
    public static class NetWorkSystem
    {

        private static Server server;

        private static Dictionary<ushort, Connection> clients;

        private static Thread updateMessageThread;

    

        private static void OnConnect(object? sender, ServerConnectedEventArgs e)
        {
            clients.TryAdd(e.Client.Id, e.Client);
        }

        private static void OnDisConnect(object? sender, ServerDisconnectedEventArgs e)
        {
            if (clients.ContainsKey(e.Client.Id))
            {
                clients.Remove(e.Client.Id);
            }
        }


        public static void Start(ushort port,ushort maxConnect)
        {
            updateMessageThread?.Abort();
            server = new Server();
            clients = new Dictionary<ushort, Connection>();
            RiptideLogger.Initialize(Console.WriteLine, false);
            server.ClientConnected += OnConnect;
            server.ClientDisconnected += OnDisConnect;
            server.Start(port,maxConnect);
            updateMessageThread = new Thread(UpdateMessage);
            updateMessageThread.Start();
        }

        private static void UpdateMessage()
        {
            while (true)
            {
                Thread.Sleep(20);
                server.Update();
            }
        }

        public static Message CreateMessage(MessageSendMode sendMode,Enum id)
        {
            return Message.Create(sendMode, id);
        }


        public static void  SendAll(Message message)
        {
            server.SendToAll(message);
        }

        public static void Send(Message message,Connection connection)
        {
            server.Send(message,connection);
        }

        public static void Send(Message message, ushort id)
        {
            server.Send(message, id);
        }





    }
}
