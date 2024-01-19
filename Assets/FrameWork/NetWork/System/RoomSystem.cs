using NetWork.Type;
using Riptide;
using UnityEngine;

namespace NetWork.System
{
    public static class RoomSystem
    {
        [MessageHandler((ushort)ServerToClientMessageType.JoinError)]
        private static void JoinError(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            Debug.Log("第"+tick+"帧"+info);
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerJoinRoom)]
        private static void PlayerJoinRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            Debug.Log("第"+tick+"帧"+id+"加入了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerLeftRoom)]
        private static void PlayerLeftRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            Debug.Log("第"+tick+"帧"+id+"离开了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Information)]
        private static void Information(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            Debug.Log("第"+tick+"帧"+info);
        }
    }
}