using System;
using FrameWork.NetWork.Component;
using NetWork.System;
using NetWork.Type;
using Riptide;
using UnityEngine;

namespace FrameWork.cs
{
    public class Player : MonoBehaviour
    {
        private Identity _identity;
        // public int tick;
        // public Vector3 selfLoc;
        // public Vector3 syncLoc;
        // public float l = 0;
        // public float lp = 0;
        public ushort id;
        // public bool isSync = false;
        public void Init(ushort id)
        {
            this.id = id;
        }
        // private void FixedUpdate()
        // {
        //     if (NetWorkSystem.GetClientId()==id)
        //     {
        //         var msg=NetWorkSystem.CreateMessage(MessageSendMode.Unreliable, ClientToServerMessageType.Transform);
        //         //msg.AddUShort(id);
        //         msg.AddVector3(transform.position);
        //         NetWorkSystem.Send(msg);
        //     }
        // }


        private void Start()
        {
            _identity = GetComponent<Identity>();
        }

        private void Update()
        {
            if (_identity.IsLocal())
            {
                var h = Input.GetAxis("Horizontal");
                var v = Input.GetAxis("Vertical");
                
                
                
                Vector3 dir = new Vector3(h, 0, v);
                transform.Translate(dir*Time.deltaTime*5,Space.World);
            }


            if (Input.GetKey(KeyCode.Space))
            {
                transform.Rotate(Vector3.one);
            }

            // if (!NetWorkSystem.GetClientId().Equals(id))
            // {
            //     transform.position = Vector3.Lerp(selfLoc, syncLoc, l);
            //     l += Time.deltaTime*10;
            // }
            
        }

        // public void SyncTransform(ushort tick,ushort id,Vector3 loc)
        // {
        //     
        //     if (NetWorkSystem.GetClientId().Equals(id))return;
        //     if(this.id!=id)return;
        //     this.tick = tick;
        //     var ti = NetWorkSystem.serverTick;
        //     if (ti>=tick&& tick>=ti-2)
        //     {
        //         selfLoc = transform.position;
        //         syncLoc = loc;
        //         l = 0;
        //     }
        // }
    }
}