using GameData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetWork.NetWork.Message
{
    
    public class MessageUdp : SingletonAsClass<MessageUdp>
    {
        private ConcurrentQueue<Data.QueueData> datas;
        public MessageUdp() { 
        
            datas = new ConcurrentQueue<Data.QueueData>();
            new Thread(Processing).Start();
        }

        public void AddData(Data.QueueData data)
        {
            datas.Enqueue(data);
        }


        private void Processing()
        {
            while (true)
            {
                Thread.Sleep(40);
                while (datas.Count > 0)
                {
                    if (datas.TryDequeue(out Data.QueueData data))
                    {
                        switch (data.data.MessageType)
                        {
                            case MessageType.Room:
                                RoomManager.Instance.RoomParseAction?.Invoke(data);
                                break;
                            case MessageType.Game:
                                Game.Instance.GameAction?.Invoke(data);
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
