using GameData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetWork.NetWork.Message
{
    public class MessageTcp: SingletonAsClass<MessageTcp>
    {
        private ConcurrentQueue<Data> datas;
        public MessageTcp()
        {
            datas = new ConcurrentQueue<Data>();
            new Thread(Processing).Start();
        }

        public void AddData(Data data)
        {
            datas.Enqueue(data);
        }

        private void Processing()
        {
            while (true)
            {
                while (datas.Count > 0)
                {
                    if (datas.TryDequeue(out Data data))
                    {
                        switch (data.MessageType)
                        {
                            case MessageType.Room:
                                RoomManager.Instance.RoomAction?.Invoke(data);
                                break;
                            case MessageType.GameLobby:
                                GameLobby.Instance.GameLobbyAction?.Invoke(data);
                                break;
                            case MessageType.Lobby:
                                lobby.Instance.lobbyAction?.Invoke(data);
                                break;
                        }
                    }
                }
            }
        }
    }
}
