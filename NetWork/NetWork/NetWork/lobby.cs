using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class lobby:SingletonAsClass<lobby>
    {
        public Action<Data> lobbyAction;
    }
}
