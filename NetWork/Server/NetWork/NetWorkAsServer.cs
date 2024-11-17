using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Riptide;

namespace FrameWork
{
    public static class NetWorkAsServer
    {
        private static Server _server;

        public static Action<object,ServerConnectedEventArgs> OnServerConnected;
        public static Action<object,ServerConnectionFailedEventArgs> OnConnectionFailed;
        public static Action<object,ServerDisconnectedEventArgs> OnClientDisconnected;
        public static Action<object,MessageReceivedEventArgs> OnMessageReceived;

        public static Action OnUpdate;
        
        public static Server Start(ushort port, ushort maxConnections)
        {
            _server = new Server();
            _server.Start(port, maxConnections);
            
            _server.ClientConnected += ((sender, args) => OnServerConnected?.Invoke(sender, args) );
            _server.ConnectionFailed += ((sender, args) => OnConnectionFailed?.Invoke(sender, args) );
            _server.ClientDisconnected += ((sender, args) => OnClientDisconnected?.Invoke(sender, args) );
            _server.MessageReceived += ((sender, args) => OnMessageReceived?.Invoke(sender, args) );
            Task.Run(Update);
            return _server;
        }


        private static async void Update()
        {
            while (true)
            {
                await Task.Delay(50);
                 _server?.Update();
                 OnUpdate?.Invoke();
            }
        }


        public static bool TryGetClient(ushort id,out Connection client)
        {
            return _server.TryGetClient(id, out client);
        }


        public static void SendToClient(ushort id, Message message)
        {
            _server.Send(message, id);
        }
        
        public static void SendToClient(ushort[] ids, Message message)
        {
            var msg = message;
            for (int i = 0; i < ids.Length; i++)
            {
                _server.Send(message, ids[i],false);
            }
            msg.Release();
        }
        
        
        public static void SendToAll(Message message)
        {
            _server.SendToAll(message);
        }


        public static void DisconnectClient(ushort id)
        {
            _server.DisconnectClient(id);
        }
        
    }
}