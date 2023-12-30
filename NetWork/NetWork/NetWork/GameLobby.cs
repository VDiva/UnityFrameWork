using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class GameLobby: SingletonAsClass<GameLobby>
    {
        /// <summary>
        /// 游戏大厅消息广播
        /// </summary>
        public Action<Data> GameLobbyAction;
    }
}
