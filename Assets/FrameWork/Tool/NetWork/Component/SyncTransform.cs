
using NetWork.Type;
using Riptide;
using UnityEngine;

namespace FrameWork
{
    public class SyncTransform : NetWorkSystemMono
    { 
        public float positionSyncSpeed=10;
        public float rotationSyncSpeed = 10;
        
        
        //private Identity _identity;
        
        private Vector3 _curLoc;
        private Vector3 _syncLoc;

        private Vector3 _curRo;
        private Vector3 _syncRo;
        
        private float _lerpPosition;
        private float _lerpRotation;

        private Vector3 _dir;


        private Vector3 _loc;
        private Vector3 _rot;
        
        private void FixedUpdate()
        {
            if (!IsLocal)return;
            
            var loc = transform.position;
            var rot = transform.eulerAngles;
            var locDis = Vector3.Distance(loc, _loc);
            var rotDis = Vector3.Distance(rot,_rot);
            
            if (locDis>0.1f||rotDis >0.1f)
            {
                var msg=NetWorkSystem.CreateMessage(MessageSendMode.Unreliable, ClientToServerMessageType.Transform);
                msg.AddUShort(GetId());
                msg.AddVector3(loc);
                msg.AddVector3(rot);
                _loc =loc;
                _rot = rot;
                NetWorkSystem.Send(msg);
            }
            
        }


        private void Update()
        {
            if (!IsLocal)
            {
                _dir = (transform.position - _syncLoc).normalized;
                transform.Translate(_dir*Time.deltaTime*positionSyncSpeed);
                transform.position = Vector3.Lerp(_curLoc, _syncLoc, _lerpPosition);
                transform.rotation=Quaternion.Lerp(Quaternion.Euler(_curRo), Quaternion.Euler(_syncRo), _lerpRotation);
                _lerpPosition += Time.deltaTime*positionSyncSpeed;
                _lerpRotation += Time.deltaTime * rotationSyncSpeed;
            }
        }

        protected override void OnTransform(ushort tick, ushort id, Vector3 position, Vector3 rotation)
        {
            base.OnTransform(tick, id, position, rotation);
            //if (NetWorkSystem.GetClientId().Equals(id))return;
            //if(GetId()!=id)return;
            if (IsLocal) return;
            var ti = NetWorkSystem.serverTick;
            if (ti>=tick&& tick>=ti-2)
            {
                //Debug.Log("同步位置事件");
                _curLoc = transform.position;
                _syncLoc = position;

                _curRo = transform.eulerAngles;
                _syncRo = rotation;
                
                _lerpPosition = 0;
                _lerpRotation = 0;
            }
        }

    }
}