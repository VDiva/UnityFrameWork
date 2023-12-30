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
        public Action<Data> RoomAction;
    }
}
