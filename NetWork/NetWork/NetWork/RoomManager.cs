using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class RoomManager : SingletonAsClass<RoomManager>
    {
        /// <summary>
        /// 房间消息广播
        /// </summary>
        public Action<Data.QueueData> RoomAction;

        public Action<Data.QueueData> RoomParseAction;

        public RoomManager()
        {
            RoomParseAction += Parse;
        }

        public void Parse(Data.QueueData data)
        {
            GameData.Data gameData = data.data;
            switch (gameData.RoomCMD)
            {
                case RoomCMD.Join:
                    break;
            }
        }

    }
}
