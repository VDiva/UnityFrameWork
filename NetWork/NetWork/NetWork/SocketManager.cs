using System.Collections.Concurrent;
using NetWork;

namespace NetWork
{
    public class SocketManager: SingletonAsClass<SocketManager>
    {
        private ConcurrentDictionary<int, Client> _sockets;

        public SocketManager()
        {
            _sockets = new ConcurrentDictionary<int, Client>();
        }

        public void AddClient(int id, Client client)
        {
            _sockets.TryAdd(id, client);
        }

        public void RemoveClient(int id)
        {
            if(_sockets.TryRemove(id, out Client client))
            {
                client.socket.Close();
                
            }
        }
        
    }
}