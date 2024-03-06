
using System;
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
        protected virtual void Start()
        {
            _identity = GetComponent<Identity>();
        }

        protected virtual void OnEnable()
        {
            NetWorkSystem.OnPlayerJoinRoom += OnPlayerJoin;
            NetWorkSystem.OnPlayerLeftRoom += OnPlayerLeft;
            NetWorkSystem.OnJoinError += OnJoinError;
            NetWorkSystem.OnInformation += OnInformation;
            NetWorkSystem.OnTransform += OnTransform;
            NetWorkSystem.OnInstantiate += OnInstantiate;
            NetWorkSystem.OnConnectToServer += OnConnected;
            NetWorkSystem.OnDisConnectToServer += OnDisConnected;
            NetWorkSystem.OnRoomInfo += OnRoomInfo;
            NetWorkSystem.OnInstantiateEnd += OnInstantiateEnd;
            NetWorkSystem.OnRpc += OnRpc;
        }

        protected virtual void OnDisable()
        {
            NetWorkSystem.OnPlayerJoinRoom -= OnPlayerJoin;
            NetWorkSystem.OnPlayerLeftRoom -= OnPlayerLeft;
            NetWorkSystem.OnJoinError -= OnJoinError;
            NetWorkSystem.OnInformation -= OnInformation;
            NetWorkSystem.OnTransform -= OnTransform;
            NetWorkSystem.OnInstantiate -= OnInstantiate;
            NetWorkSystem.OnConnectToServer -= OnConnected;
            NetWorkSystem.OnDisConnectToServer -= OnDisConnected;
            NetWorkSystem.OnRoomInfo -= OnRoomInfo;
            NetWorkSystem.OnInstantiateEnd -= OnInstantiateEnd;
            NetWorkSystem.OnRpc -= OnRpc;
        }
        
        protected void RpcMessage(Action<object[]> methodInfo,object[] param=null)
        {
            var methodName=methodInfo.Method.Name;
            var mAttribute=methodInfo.Method.GetCustomAttribute<NetTypeAttribute>();
            if (mAttribute!=null)
            {
                NetWorkSystem.Rpc(methodName,this,mAttribute.Rpc,param);
            }
        }
        
        protected void RpcMessage(Action<object[]> methodInfo,NetWorkSystemMono netWorkSystemMono,object[] param=null)
        {
            var methodName=methodInfo.Method.Name;
            var mAttribute=methodInfo.Method.GetCustomAttribute<NetTypeAttribute>();
            if (mAttribute!=null)
            {
                NetWorkSystem.Rpc(methodName,netWorkSystemMono,mAttribute.Rpc,param);
            }
        }
        

        protected virtual void OnPlayerJoin(ushort id, int roomId,string roomName){}

        protected virtual void OnPlayerLeft(ushort id){}
        protected virtual void OnJoinError(string info){}

        protected virtual void OnInformation(string info){}

        protected virtual void OnTransform(ushort tick, ushort id, Vector3 position, Vector3 rotation){}

        
        protected virtual void OnRoomInfo(ushort currentCount,ushort maxCount){}
        
        protected virtual void OnConnected(){}
        
        
        protected virtual void OnDisConnected(){}
        
        protected virtual void OnInstantiate(ushort id,ushort objId,string spawnName,Vector3 position,Vector3 rotation,bool isAb){}

        protected virtual void OnInstantiateEnd(GameObject go){}
        
        public ushort GetId()
        {
            return _identity.GetObjId();
        }

        protected virtual void OnRpc(string methodName, ushort id, object[] param)
        {
            if (GetId().Equals(id))
            {
                gameObject.SendMessage(methodName,param==null? new object[1]: param);
            }
        }
        
    }
}