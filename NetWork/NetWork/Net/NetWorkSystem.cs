
using NetWork.Type;
using Riptide;
using Riptide.Utils;

namespace NetWork
{
    public static class NetWorkSystem
    {

        private static Server server;

        private static Dictionary<ushort, Connection> clients;

        private static Thread updateMessageThread;

        private static Thread tickThread;

        private static ushort currentTick;
        private static void OnConnect(object? sender, ServerConnectedEventArgs e)
        {
            clients.TryAdd(e.Client.Id, e.Client);
        }

        private static void OnDisConnect(object? sender, ServerDisconnectedEventArgs e)
        {
            if (clients.ContainsKey(e.Client.Id))
            {
                clients.Remove(e.Client.Id);
                RoomSystem.PlayerDisConnect(e.Client.Id);
            }
        }


        public static void Start(ushort port,ushort maxConnect)
        {
            updateMessageThread?.Abort();
            tickThread?.Abort();
            server = new Server();
            clients = new Dictionary<ushort, Connection>();
            RiptideLogger.Initialize(Console.WriteLine, false);
            server.ClientConnected += OnConnect;
            server.ClientDisconnected += OnDisConnect;
            server.Start(port,maxConnect);
            updateMessageThread = new Thread(UpdateMessage);
            updateMessageThread.Start();

            tickThread = new Thread(UpdateTick);
            tickThread.Start();
        }

        private static void UpdateMessage()
        {
            while (true)
            {
                Thread.Sleep(100);
                try
                {
                    server.Update();
                }catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private static void UpdateTick()
        {
            while (true)
            {
                Thread.Sleep(1000);
                currentTick += 1; 
                SendTick();
            }
        }

        private static void SendTick()
        {
            var msg = CreateMessage(MessageSendMode.Unreliable, ServerToClientMessageType.SyncTick);
            msg.AddUShort(currentTick);
            try
            {
                server.SendToAll(msg);
            }catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }


        public static Message CreateMessage(MessageSendMode sendMode,Enum id)
        {
            Message msg = Message.Create(sendMode, id);
            msg.AddUShort(currentTick);
            return msg;
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

        public static ushort GetTick()
        {
            return currentTick;
        }


        public static Connection GetClient(ushort id)
        {
            return clients[id];
        }

        public static void SendError(string info,ushort id)
        {
            Message msg=CreateMessage(MessageSendMode.Reliable,ServerToClientMessageType.Information);
            msg.AddString(info);
            Send(msg,id);
        }

    }
}
