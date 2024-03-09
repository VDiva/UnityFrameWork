
using Newtonsoft.Json;
using Riptide;

namespace FrameWork
{
    public static class MessageSystem
    {
        [MessageHandler((ushort)ServerToClientMessageType.JoinError)]
        private static void JoinError(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.JoinError,new object[]{info});
            //NetWorkSystem.OnJoinError?.Invoke(info);
            //Debug.Log("第"+tick+"帧"+info);
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerJoinRoom)]
        private static void PlayerJoinRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            var roomId = message.GetInt();
            var roomName = message.GetString();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.PlayerJoinRoom,new object[]{id,roomId,roomName});
            //NetWorkSystem.OnPlayerJoinRoom?.Invoke(id,roomId,roomName);
            //Debug.Log("第"+tick+"帧"+id+"加入了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerLeftRoom)]
        private static void PlayerLeftRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.PlayerLeftRoom,new object[]{id});
            //NetWorkSystem.OnPlayerLeftRoom?.Invoke(id);
            //Debug.Log("第"+tick+"帧"+id+"离开了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Information)]
        private static void Information(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.Information,new object[]{info});
            //NetWorkSystem.OnJoinError?.Invoke(info);
            //Debug.Log("第"+tick+"帧"+info);
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Transform)]
        private static void Transform(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            var loc = message.GetVector3();
            var ro = message.GetVector3();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.Transform,new object[]{tick,id,loc,ro});
            //NetWorkSystem.OnTransform?.Invoke(tick,id,loc,ro);
            //Debug.Log(id+"-"+loc+"-"+tick);
        }
        
        
        [MessageHandler((ushort)ServerToClientMessageType.Instantiate)]
        private static void Instantiate(Message message)
        {
            var tick = message.GetUShort();
            var clientId = message.GetUShort();
            var objId = message.GetUShort();
            var packName = message.GetString();
            var spawnName = message.GetString();
            var typeName = message.GetString();
            var loc = message.GetVector3();
            var ro = message.GetVector3();
            var isAb = message.GetBool();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.Instantiate,new object[]{clientId,objId,packName,spawnName,typeName,loc,ro,isAb});
            //NetWorkSystem.OnInstantiate?.Invoke(clientId,objId,spawnName,loc,ro,isAb);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Rpc)]
        private static void Rpc(Message message)
        {
            var tick = message.GetUShort();
            var methodName = message.GetString();
            var objId = message.GetUShort();
            var param = message.GetString();
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.Rpc,new object[]{message,objId,JsonConvert.DeserializeObject<object[]>(param)});
            //NetWorkSystem.OnRpc?.Invoke(methodName,objId,JsonConvert.DeserializeObject<object[]>(param));
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.SetBelongingClient)]
        private static void SetBelongingClient(Message message)
        {
            var tick = message.GetUShort();
            var newId = message.GetUShort();
            var count = message.GetInt();
            var ids = message.GetUShorts(count);
            EventManager.DispatchEvent(MessageType.NetMessage, NetMessageType.BelongingClient, new object[] { newId,ids });
            //NetWorkSystem.OnBelongingClient?.Invoke(newId,ids);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Destroy)]
        private static void Destroy(Message message)
        {
            var tick = message.GetUShort();
            var objId = message.GetUShort();
            EventManager.DispatchEvent(MessageType.NetMessage, NetMessageType.Destroy, new object[] { objId });
            //NetWorkSystem.OnDestroy?.Invoke(objId);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.GetRoomInfo)]
        private static void GetRoomInfo(Message message)
        {
            var tick = message.GetUShort();
            var curCount = message.GetUShort();
            var maxCount = message.GetUShort();
            EventManager.DispatchEvent(MessageType.NetMessage, NetMessageType.RoomInfo, new object[] { curCount,maxCount });
            //NetWorkSystem.OnRoomInfo?.Invoke(curCount,maxCount);
            //Debug.Log("生成玩家");
        }
        
        
    }
}