using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class GameLobby: SingletonAsClass<GameLobby>
    {
        public Action<Data> GameLobbyAction;
    }
}
