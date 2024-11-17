using System;
using FrameWork;
using Riptide;
using UnityEngine;

namespace Cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            NetWork.InitLog(Debug.Log);
            NetWorkAsClient.Connect("127.0.0.1",8888);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                var msg=NetWork.GetMsg(MessageSendMode.Reliable, 3);
                msg.AddString("大傻逼");
                msg.AddUShort(10);
                msg.AddString("");
                NetWorkAsClient.Send(msg);
                
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                var msg=NetWork.GetMsg(MessageSendMode.Reliable, 0);
                NetWorkAsClient.Send(msg);
            }
        }

        private void FixedUpdate()
        {
            NetWorkAsClient.Update();
        }
    }
}