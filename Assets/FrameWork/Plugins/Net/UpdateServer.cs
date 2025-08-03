using System;
using NetWorkClient;
using UnityEngine;

namespace FrameWork
{
    public class UpdateServer : SingletonAsMono<UpdateServer>
    {
        private void FixedUpdate()
        {
            NetClient.Update();
        }
    }
}