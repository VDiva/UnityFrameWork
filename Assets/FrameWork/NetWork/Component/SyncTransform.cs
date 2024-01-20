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
        public float range=0.05f;
        
        
        private Identity _identity;
        private Vector3 _curLoc;
        private Vector3 _syncLoc;

        private Vector3 _curRo;
        private Vector3 _syncRo;
        private float _lerpPosition;
        private float _lerpRotation;


        private Vector3 _loc;
        
        private float _sqRange;
        
        private void Awake()
        {
            _identity = GetComponent<Identity>();
            _sqRange = range * range;
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
            
            if (_identity.IsLocalSpawn())
            {
                var msg=NetWorkSystem.CreateMessage(MessageSendMode.Unreliable, ClientToServerMessageType.Transform);
                msg.AddUShort(_identity.GetId());
                msg.AddVector3(transform.position);
                msg.AddVector3(transform.eulerAngles);
                //_loc = loc;
                NetWorkSystem.Send(msg);
            }
        }


        private void Update()
        {
            if (!_identity.IsLocalSpawn())
            {
                if (((_curLoc-_syncLoc).normalized).sqrMagnitude>_sqRange)
                {
                    transform.position = Vector3.Lerp(_curLoc, _syncLoc, _lerpPosition);
                    
                    _lerpPosition += Time.deltaTime*positionSyncSpeed;
                    //_lerpRotation += Time.deltaTime * rotationSyncSpeed;
                }

                if (((_curRo-_syncRo).normalized).sqrMagnitude>_sqRange)
                {
                    transform.eulerAngles = _syncRo;
                }
            }
        }
        
        
        private void SyncLoc(ushort tick,ushort id,Vector3 loc,Vector3 ro)
        {
            //if (NetWorkSystem.GetClientId().Equals(id))return;
            if(_identity.GetId()!=id)return;
            if (_identity.IsLocalSpawn()) return;
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