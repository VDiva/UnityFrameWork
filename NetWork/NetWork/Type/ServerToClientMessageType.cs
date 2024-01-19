using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetWork.Type
{
    public enum ServerToClientMessageType: ushort
    {
        SyncTick=1,
        JoinError=4,
        PlayerJoinRoom=5,
        PlayerLeftRoom=6,
        Information=7,
        JoinRoomSuccess=8,
        Transform=9,
    }
}
