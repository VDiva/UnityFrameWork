using System;
using NetWork.System;
using Riptide;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            NetWorkSystem.Start("127.0.0.1:8888");
        }

        private void Update()
        {
            // var message=NetWorkSystem.CreateMessage(MessageSendMode.Reliable, clien);
            // message.AddString("消息:"+Random.Range(0, 100));
            // NetWorkSystem.Send(message);
        }
    }
}