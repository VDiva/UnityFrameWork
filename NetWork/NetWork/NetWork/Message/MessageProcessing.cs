using GameData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork.Message
{
    public class MessageProcessing: SingletonAsClass<MessageProcessing>
    {
        private ConcurrentQueue<Data> datas;
        public MessageProcessing() { 
        
            datas = new ConcurrentQueue<Data>();
        }


        public void AddData(Data data)
        {
            if (data == null)
            {

            }
        }



    }
}
