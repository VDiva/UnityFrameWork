namespace FrameWork
{
    public enum MessageType
    {
        NetMessage=1,//网络消息
        UiMessage=2,//ui消息,
        Animation=3 //动画消息
    }
    
    
    public enum NetMessageType
    {
        PlayerJoinRoom=1,//玩家加入房间
        PlayerLeftRoom=2,//玩家离开房间
        JoinError=3,//加入异常
        Information=4,//字符串消息
        Transform=5,//位置同步消息
        Instantiate=6,//生成物体消息
        BelongingClient=7,//归属客户端切换消息
        Rpc=8,//Rpc消息
        ConnectToServer=9,//链接到服务器消息
        DisConnectToServer=10,//断开服务器消息
        Destroy=11,//销毁
        RoomInfo=12,//房间信息
        InstantiateEnd,
        ReLink
    }
    
    public enum UiMessageType
    {
        Show,
        Hide,
        Remove
    }

    public enum AnimMessageType
    {
        Start=1,
        End=2
    }
}