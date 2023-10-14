using System.Collections.Concurrent;
using FrameWork.Singleton;

namespace FrameWork.NetManager.Socket
{
    public class SocketManager: SingletonAsClass<SocketManager>
    {
        private ConcurrentDictionary<int, Client> _sockets;

        public SocketManager()
        {
            _sockets = new ConcurrentDictionary<int, Client>();
        }

        public void AddClient(int id,Client client)
        {
            _sockets.TryAdd(id, client);
        }
        
    }
}