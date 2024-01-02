using System;
using System.Collections.Generic;
using System.Text;
using FrameWork.ObjectPool;
using GameData;

namespace NetWork.NetWork.Message
{
    public static class MessageData
    {
        public static ObjectPool<GameData.Data> GameData = new ObjectPool<GameData.Data> (Init);
        public static ObjectPool<Data.QueueData> QueueData = new ObjectPool<Data.QueueData> (Init);


        private static void Init(GameData.Data data)
        {
            data.TransfromData=new TransfromData();
            data.RoomData = new RoomData();
        }

        private static void Init(Data.QueueData data)
        {
            
        }
    }
}
