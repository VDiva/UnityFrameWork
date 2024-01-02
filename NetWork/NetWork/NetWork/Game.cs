using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class Game: SingletonAsClass<Game>
    {
        /// <summary>
        /// 游戏大厅消息广播
        /// </summary>
        public Action<Data.QueueData> GameAction;
    }
}
