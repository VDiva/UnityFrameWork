using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class RoomManager : SingletonAsClass<RoomManager>
    {
        public Action<Data> RoomAction;
    }
}
