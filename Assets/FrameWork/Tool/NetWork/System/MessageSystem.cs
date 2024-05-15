
using NetWork.Type;
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
            var data = EventManager.GetEventMsg();
            data.Add(info);
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.JoinError,data);
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
            
            var data = EventManager.GetEventMsg();
            data.Add(id);
            data.Add(roomId);
            data.Add(roomName);
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.PlayerJoinRoom,data);
            //NetWorkSystem.OnPlayerJoinRoom?.Invoke(id,roomId,roomName);
            //Debug.Log("第"+tick+"帧"+id+"加入了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.PlayerLeftRoom)]
        private static void PlayerLeftRoom(Message message)
        {
            var tick = message.GetUShort();
            var id = message.GetUShort();
            var data = EventManager.GetEventMsg();
            data.Add(id);
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.PlayerLeftRoom,data);
            //NetWorkSystem.OnPlayerLeftRoom?.Invoke(id);
            //Debug.Log("第"+tick+"帧"+id+"离开了房间");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Information)]
        private static void Information(Message message)
        {
            var tick = message.GetUShort();
            var info = message.GetString();
            var data = EventManager.GetEventMsg();
            data.Add(info);
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.Information,data);
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
            var data = EventManager.GetEventMsg();
            data.Add(tick);
            data.Add(id);
            data.Add(loc);
            data.Add(ro);
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.Transform,data);
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
            
            var data = EventManager.GetEventMsg();
            data.Add(clientId);
            data.Add(objId);
            data.Add(packName);
            data.Add(spawnName);
            data.Add(loc);
            data.Add(ro);
            data.Add(isAb);
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.Instantiate,data);
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
            
            var data = EventManager.GetEventMsg();
            data.Add(message);
            data.Add(objId);
            data.Add(JsonConvert.DeserializeObject<object[]>(param));
            EventManager.DispatchEvent((int)MessageType.NetMessage,(int)NetMessageType.Rpc,data);
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
            
            var data = EventManager.GetEventMsg();
            data.Add(newId);
            data.Add(ids);
            EventManager.DispatchEvent((int)MessageType.NetMessage, (int)NetMessageType.BelongingClient,data);
            //NetWorkSystem.OnBelongingClient?.Invoke(newId,ids);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.Destroy)]
        private static void Destroy(Message message)
        {
            var tick = message.GetUShort();
            var objId = message.GetUShort();
            var data = EventManager.GetEventMsg();
            data.Add(objId);
            
            EventManager.DispatchEvent((int)MessageType.NetMessage, (int)NetMessageType.Destroy, data);
            //NetWorkSystem.OnDestroy?.Invoke(objId);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.GetRoomInfo)]
        private static void GetRoomInfo(Message message)
        {
            var tick = message.GetUShort();
            var curCount = message.GetUShort();
            var maxCount = message.GetUShort();
            
            var data = EventManager.GetEventMsg();
            data.Add(curCount);
            data.Add(maxCount);
            EventManager.DispatchEvent((int)MessageType.NetMessage, (int)NetMessageType.RoomInfo, data);
            //NetWorkSystem.OnRoomInfo?.Invoke(curCount,maxCount);
            //Debug.Log("生成玩家");
        }
        
        [MessageHandler((ushort)ServerToClientMessageType.ReLink)]
        private static void ReLink(Message message)
        {
            var tick = message.GetUShort();
            var newId = message.GetUShort();
            var oldId = message.GetUShort();
            
            var data = EventManager.GetEventMsg();
            data.Add(newId);
            data.Add(oldId);
            EventManager.DispatchEvent((int)MessageType.NetMessage, (int)NetMessageType.ReLink, data);
            //NetWorkSystem.OnRoomInfo?.Invoke(curCount,maxCount);
            //Debug.Log("生成玩家");
        }
        
        
    }
}