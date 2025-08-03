using System;

namespace Library.Msg
{
    
    
    public enum MsgType: ushort
    {
        Update=1,
        Room=100,
        StrMsg=200,
    }

    public enum RoomType: ushort
    {
        JoinRoom=1000,
        LeaveRoom=1001,
        Retransmission=1002,
        CreateRoom=1003,
        MatchingRoom=1004,
        
        
        RoomRecycle=1500,
        InputData=2000,
        
    }

    public enum StrMsgType: ushort
    {
        NorStr,
    }
}