namespace FrameWork
{
    public enum MessageType
    {
        NetMsg
    }

    public enum NetMsgType: ushort
    {
        LeaveRoom,
        JoinRoom,
        JoinRoomFailed,
        CreateRoom,
        Msg
    }
    
    
}