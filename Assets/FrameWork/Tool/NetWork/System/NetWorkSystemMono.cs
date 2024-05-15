
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrameWork
{
    [RequireComponent(typeof(Identity))]
    public class NetWorkSystemMono : MonoBehaviour
    {
        private Identity _identity;
        
        protected bool IsLocal
        {
            get => _identity.IsLocalSpawn();
        }

        private void Awake()
        {
            _identity = GetComponent<Identity>();
        }
        
        protected virtual void OnEnable()
        {
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.PlayerJoinRoom,OnPlayerJoin);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.PlayerLeftRoom,OnPlayerLeft);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.JoinError,OnJoinError);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.Information,OnInformation);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.Transform,OnTransform);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.Instantiate,OnInstantiate);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.ConnectToServer,OnConnected);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.DisConnectToServer,OnDisConnected);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.RoomInfo,OnRoomInfo);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.InstantiateEnd,OnInstantiateEnd);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.Rpc,OnRpc);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.BelongingClient,SetBelongingClient);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.Destroy,OnNetDestroy);
            EventManager.AddListener((int)MessageType.NetMessage,(int)NetMessageType.ReLink,OnReLink);
            
            
        }

        protected virtual void OnDisable()
        {
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.PlayerJoinRoom,OnPlayerJoin);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.PlayerLeftRoom,OnPlayerLeft);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.JoinError,OnJoinError);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.Information,OnInformation);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.Transform,OnTransform);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.Instantiate,OnInstantiate);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.ConnectToServer,OnConnected);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.DisConnectToServer,OnDisConnected);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.RoomInfo,OnRoomInfo);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.InstantiateEnd,OnInstantiateEnd);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.Rpc,OnRpc);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.BelongingClient,SetBelongingClient);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.Destroy,OnNetDestroy);
            EventManager.RemoveListener((int)MessageType.NetMessage,(int)NetMessageType.ReLink,OnReLink);
        }
        
        private void OnPlayerJoin(List<object> param) {OnPlayerJoin((ushort)param[0],(int)param[1],(string)param[2]); }
        protected virtual void OnPlayerJoin(ushort id, int roomId,string roomName){}

        private void OnPlayerLeft(List<object> param) { OnPlayerLeft((ushort)param[0]); }
        protected virtual void OnPlayerLeft(ushort id){}
        
        private void OnJoinError(List<object> param) { OnJoinError((string)param[0]); }
        protected virtual void OnJoinError(string info){}

        private void OnInformation(List<object> param) { OnInformation((string)param[0]); }
        protected virtual void OnInformation(string info){}

        private void OnTransform(List<object> param) { OnTransform((ushort)param[0],(ushort)param[1],(Vector3)param[2],(Vector3)param[3]); }
        protected virtual void OnTransform(ushort tick, ushort id, Vector3 position, Vector3 rotation){}
        
        private void OnRoomInfo(List<object> param) { OnRoomInfo((ushort)param[0],(ushort)param[1]); }
        protected virtual void OnRoomInfo(ushort currentCount,ushort maxCount){}
        
        private void OnConnected(List<object> param){OnConnected(); }
        protected virtual void OnConnected(){}
        
        private void OnDisConnected(List<object> param) {OnDisConnected();}
        protected virtual void OnDisConnected(){}
        
        private void OnInstantiate(List<object> param) { OnInstantiate((ushort)param[0],(ushort)param[1],(string)param[2],(string)param[3],(string)param[4],(Vector3)param[5],(Vector3)param[6],(bool)param[7]); }
        protected virtual void OnInstantiate(ushort id,ushort objId,string packName,string spawnName,string typeName,Vector3 position,Vector3 rotation,bool isAb){}

        private void OnInstantiateEnd(List<object> param) { OnInstantiateEnd((GameObject)param[0]); }
        protected virtual void OnInstantiateEnd(GameObject go){}
        
        public ushort GetId() {return _identity.GetObjId();}
        
        private void OnRpc(List<object> param) { OnRpc((string)param[0],(ushort)param[1],(object[])param[2]); }
        private void OnRpc(string methodName, ushort id, object[] param) {if (GetId().Equals(id))gameObject.SendMessage(methodName,param==null? new object[1]: param); }
        
        private void SetBelongingClient(List<object> param) { SetBelongingClient((ushort)param[0],(ushort[])param[1]); }

        private void SetBelongingClient(ushort newId, ushort[] ids) {if (ids.Contains(GetId())&& _identity.GetObjId()!=_identity.GetClientId())_identity.SetClientId(newId); }
        
        private void OnNetDestroy(List<object> param) { OnNetDestroy((ushort)param[0]); }
        protected virtual void OnNetDestroy(ushort objId) {if (GetId().Equals(objId))Destroy(gameObject);}
        
        private void OnReLink(List<object> param) { OnReLink((ushort)param[0],(ushort)param[1]); }

        protected virtual void OnReLink(ushort newId, ushort oldId)
        {
            if (_identity.GetObjId() == _identity.GetClientId()&&_identity.GetClientId().Equals(oldId))
            {
                _identity.SetClientId(newId); 
                _identity.SetObjId(newId);
            }
        }
        
    }
}