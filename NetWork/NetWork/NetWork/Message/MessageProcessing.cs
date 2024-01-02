using GameData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetWork.NetWork.Message
{
    public class MessageProcessing: SingletonAsClass<MessageProcessing>
    {
        private ConcurrentQueue<Data.QueueData> datas;
        public MessageProcessing() { 
        
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
                while (datas.Count>0)
                {
                    if(datas.TryDequeue(out Data.QueueData data))
                    {
                        
                        switch(data.data.MessageProtocol)
                        {
                            case MessageProtocol.Tcp:
                                MessageTcp.Instance.AddData(data);
                                break;
                            case MessageProtocol.Udp:
                                MessageUdp.Instance.AddData(data);
                                break;
                        }
                    }
                }
            }
        }

    }
}
