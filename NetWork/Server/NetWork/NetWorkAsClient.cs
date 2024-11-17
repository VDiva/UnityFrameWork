using System;
using Riptide;

namespace FrameWork
{
    public static class NetWorkAsClient
    {
        private static Client _client;

        public static Action<object,ClientConnectedEventArgs> OnClientConnected;
        public static Action<object,ClientDisconnectedEventArgs> OnClientDisconnected;
        public static Action<object,EventArgs> OnConnected;
        public static Action<object,DisconnectedEventArgs> OnDisconnected;
        public static Action<object,ConnectionFailedEventArgs> OnConnectionFailed;
        public static Action<object,MessageReceivedEventArgs> OnMessageReceived;

        
        public static void Connect(string host, int port)
        {
            _client = new Client();
            _client.ClientConnected += ((sender, args) => OnClientConnected?.Invoke(sender,args));
            _client.ClientDisconnected += ((sender, args) => OnClientDisconnected?.Invoke(sender,args));
            _client.Connected += ((sender, args) => OnConnected?.Invoke(sender,args));
            _client.Disconnected += ((sender, args) => OnDisconnected?.Invoke(sender,args));
            _client.ConnectionFailed+=((sender, args) => OnConnectionFailed?.Invoke(sender,args));
            _client.MessageReceived += ((sender, args) => OnMessageReceived?.Invoke(sender,args));
            _client.Connect(host+":" + port);
        }
        
        public static  void Update()
        {
            _client?.Update();
        }


        public static Message GetMsg(MessageSendMode sendMode,Enum id)
        {
            var msg = NetWork.GetMsg(sendMode, id);
            msg.AddUShort(_client.Id);
            return msg;
        }
        
        public static Message GetMsg(MessageSendMode sendMode,ushort id)
        {
            var msg = NetWork.GetMsg(sendMode, id);
            msg.AddUShort(_client.Id);
            return msg;
        }
        
        public static void Send(Message message)
        {
            _client?.Send(message);
        }
    }
}