using System;
using NetWork.System;
using NetWork.Type;
using Riptide;
using UnityEngine;
using UnityEngine.Serialization;

namespace FrameWork.NetWork.Component
{
    [RequireComponent(typeof(Identity))]
    public class SyncTransform : MonoBehaviour
    { 
        public float positionSyncSpeed=10;
        public float rotationSyncSpeed = 1;
        
        private Identity _identity;
        private Vector3 _curLoc;
        private Vector3 _syncLoc;

        private Vector3 _curRo;
        private Vector3 _syncRo;
        private float _lerpPosition;
        private float _lerpRotation;


        private Vector3 _loc;
        
        private void Awake()
        {
            _identity = GetComponent<Identity>();
        }

        private void OnEnable()
        {
            NetWorkSystem.OnTransform += SyncLoc;
        }


        private void OnDisable()
        {
            NetWorkSystem.OnTransform -= SyncLoc;
        }


        private void FixedUpdate()
        {
            if (NetWorkSystem.GetClientId()==_identity.GetId())
            {
                var msg=NetWorkSystem.CreateMessage(MessageSendMode.Unreliable, ClientToServerMessageType.Transform);
                msg.AddVector3(transform.position);
                msg.AddVector3(transform.eulerAngles);
                NetWorkSystem.Send(msg);
            }
        }


        private void Update()
        {
            if (!_identity.IsLocal())
            {
                transform.position = Vector3.Lerp(_curLoc, _syncLoc, _lerpPosition);
                transform.eulerAngles = _syncRo;
                _lerpPosition += Time.deltaTime*positionSyncSpeed;
                _lerpRotation += Time.deltaTime * rotationSyncSpeed;
            }
        }
        
        
        private void SyncLoc(ushort tick,ushort id,Vector3 loc,Vector3 ro)
        {
            if (NetWorkSystem.GetClientId().Equals(id))return;
            if(_identity.GetId()!=id)return;
            var ti = NetWorkSystem.serverTick;
            if (ti>=tick&& tick>=ti-2)
            {
                _curLoc = transform.position;
                _syncLoc = loc;

                _curRo = transform.eulerAngles;
                _syncRo = ro;
                
                _lerpPosition = 0;
                _lerpRotation = 0;
            }
        }
    }
}