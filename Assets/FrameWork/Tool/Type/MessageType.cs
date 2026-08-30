namespace FrameWork
{
    public enum MessageType
    {
        NetMessage=1,//网络消息
        UiMessage=2,//ui消息,
        Animation=3, //动画消息
        Game=4
    }

    public enum GameMessageType
    {
        BeAttack=1,//被攻击
        SelectServer=2,//选择服务器
        JoyChange=3,//遥感移动
        UpdateSkin=4,//更新时装
        UpdateLanguage=5,//更新语言
        MarkTarget=6,//玩家标记了一个怪物
        UpdateSkillCd=7,//更新技能cd
        SpawnBoss=8,//生成boss
        UpdateEquip=9,//刷新装备
        UpdateSkill=10,//刷新技能
        UpdateChat=11,//更新聊天消息
        UpdateShop,//刷新商店
        UpdateHeiShiShop,//刷新黑市商店
        PlayerDie,//玩家死亡
        WatchToNextPlayer,//视角切换到下一个玩家
        WatchToLastPlayer,//视角切换到上一个玩家
        ResetPlayer,//复活玩家
        SpeedCut,//减速
        BeAttackInterval,//持续伤害
        BeDonJie,//冻结
        MonsterDie,//怪物死亡
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