using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class lobby:SingletonAsClass<lobby>
    {
        /// <summary>
        /// 大厅消息广播
        /// </summary>
        public Action<Data> lobbyAction;
    }
}
