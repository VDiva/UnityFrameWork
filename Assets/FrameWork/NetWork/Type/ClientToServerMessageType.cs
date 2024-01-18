using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetWork.Type
{
    public enum ClientToServerMessageType : ushort
    {
        Login = 1,
        Logout = 2,
        MatchingRoom = 3,
        CreateRoom = 4,
        JoinRoom = 5,
        Room=6,
        TransfromAll,
        TransfromOther,
    }
}
