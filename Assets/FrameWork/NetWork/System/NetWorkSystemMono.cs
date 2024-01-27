
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
        }


        protected void RpcMessage(string methodName,Rpc rpc,object[] param)
        {
            NetWorkSystem.Rpc(methodName,this,rpc,param);
        }
        
        protected void RpcMessage(string methodName,NetWorkSystemMono netWorkSystemMono,Rpc rpc,object[] param)
        {
            NetWorkSystem.Rpc(methodName,netWorkSystemMono,rpc,param);
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

        public ushort GetId()
        {
            return _identity.GetId();
        }
        
    }
}