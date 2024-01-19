using System;
using NetWork.System;
using NetWork.Type;
using Riptide;
using UnityEngine;

namespace FrameWork.cs
{
    public class Player : MonoBehaviour
    {
        private Vector3 selfLoc;
        private Vector3 syncLoc;
        private float l = 0;
        private ushort id;
        private bool isSync = false;
        public void Init(ushort id)
        {
            this.id = id;
        }
        private void FixedUpdate()
        {
            if (NetWorkSystem.GetClientId()==id)
            {
                var msg=NetWorkSystem.CreateMessage(MessageSendMode.Unreliable, ClientToServerMessageType.TransformOther);
                msg.AddUShort(id);
                msg.AddVector3(transform.position);
                NetWorkSystem.Send(msg);
            }
        }

        private void Update()
        {
            if (id==NetWorkSystem.GetClientId())
            {
                var h = Input.GetAxis("Horizontal");
                var v = Input.GetAxis("Vertical");
                Vector3 dir = new Vector3(h, 0, v);
                transform.Translate(dir*Time.deltaTime*5,Space.World);
            }

            if (!isSync)return;
            transform.position = Vector3.Lerp(selfLoc, syncLoc, l);
            l += Time.deltaTime;
        }

        public void SyncTransform(ushort tick,ushort id,Vector3 loc)
        {
            
            if (this.id!=id)return;
            var ti = NetWorkSystem.serverTick;
            if (ti>tick&& tick>ti-2)
            {
                isSync = true;
                selfLoc = transform.position;
                syncLoc = loc;
                l = 0;
            }
        }
    }
}