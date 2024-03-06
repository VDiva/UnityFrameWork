
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace FrameWork
{
    public class NetWork : SingletonAsMono<NetWork>
    {
        
        
        private void Awake()
        {
            DontDestroyOnLoad(this);
        }
        
        private void OnApplicationQuit()
        {
            NetWorkSystem.DisConnect();
        }

        private void FixedUpdate()
        {
            NetWorkSystem.UpdateMessage();
        }

    }
}