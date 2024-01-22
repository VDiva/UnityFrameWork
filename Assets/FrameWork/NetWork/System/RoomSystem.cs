
using Newtonsoft.Json;
using Riptide;

namespace FrameWork
{
    public static class RoomSystem
    {
        [MessageHandler((ushort)ServerToClientMessageType.JoinError)]
        private static void JoinError(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            NetWorkSystem.OnJoinError?.Invoke(info);
            //Debug.Log("第"+tick+"帧"+info);
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerJoinRoom)]
        private static void PlayerJoinRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            var roomId = message.GetInt();
            var roomName = message.GetString();
            NetWorkSystem.OnPlayerJoinRoom?.Invoke(id,roomId,roomName);
            //Debug.Log("第"+tick+"帧"+id+"加入了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerLeftRoom)]
        private static void PlayerLeftRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            NetWorkSystem.OnPlayerLeftRoom?.Invoke(id);
            //Debug.Log("第"+tick+"帧"+id+"离开了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Information)]
        private static void Information(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            NetWorkSystem.OnJoinError?.Invoke(info);
            //Debug.Log("第"+tick+"帧"+info);
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Transform)]
        private static void Transform(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            var loc = message.GetVector3();
            var ro = message.GetVector3();
            NetWorkSystem.OnTransform?.Invoke(tick,id,loc,ro);
            //Debug.Log(id+"-"+loc+"-"+tick);
        }
        
        
        [MessageHandler((ushort)ServerToClientMessageType.Instantiate)]
        private static void Instantiate(Message message)
        {
            var tick = message.GetUShort();
            var clientId = message.GetUShort();
            var objId = message.GetUShort();
            var spawnName = message.GetString();
            var loc = message.GetVector3();
            var ro = message.GetVector3();
            var isAb = message.GetBool();
            NetWorkSystem.OnInstantiate?.Invoke(clientId,objId,spawnName,loc,ro,isAb);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Rpc)]
        private static void Rpc(Message message)
        {
            var tick = message.GetUShort();
            var methodName = message.GetString();
            var objId = message.GetUShort();
            var param = message.GetString();
            
            NetWorkSystem.OnRpc?.Invoke(methodName,objId,JsonConvert.DeserializeObject<object[]>(param));
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.SetBelongingClient)]
        private static void SetBelongingClient(Message message)
        {
            var tick = message.GetUShort();
            var newId = message.GetUShort();
            var count = message.GetInt();
            var ids = message.GetUShorts(count);
            NetWorkSystem.OnBelongingClient?.Invoke(newId,ids);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Destroy)]
        private static void Destroy(Message message)
        {
            var tick = message.GetUShort();
            var objId = message.GetUShort();
            NetWorkSystem.OnDestroy?.Invoke(objId);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.GetRoomInfo)]
        private static void GetRoomInfo(Message message)
        {
            var tick = message.GetUShort();
            var curCount = message.GetUShort();
            var maxCount = message.GetUShort();
            NetWorkSystem.OnRoomInfo?.Invoke(curCount,maxCount);
            //Debug.Log("生成玩家");
        }
        
        
    }
}