using System;
using NetWorkClient;
using Riptide;
using UnityEngine;

namespace FrameWork.Plugins.Net
{
    [RequireComponent(typeof(Identity))]
    public class NetBehaviour : MonoBehaviour
    {
        private Identity _identity;

        public virtual void Awake()
        {
            _identity = GetComponent<Identity>();
        }

        public virtual void OnEnable()
        {
            NetClient.JoinRoomAction += JoinRoom;
            NetClient.LeaveRoomAction += LeaveRoom;
            NetClient.RetransmissionAction += Retransmission;
        }

        public virtual void OnDisable()
        {
            NetClient.JoinRoomAction -= JoinRoom;
            NetClient.LeaveRoomAction -= LeaveRoom;
            NetClient.RetransmissionAction -= Retransmission;
        }


        protected virtual void JoinRoom(ushort id)
        {
            
        }
        
        protected virtual void LeaveRoom(ushort id)
        {
            
        }
        
        protected virtual void Retransmission(Message msg)
        {
            
        }


        protected bool IsLocal()
        {
            return _identity.IsLocal();
        }
        
        
    }
}