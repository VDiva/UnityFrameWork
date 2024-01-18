using System;
using FrameWork.Singleton;
using UnityEditor;
using UnityEngine;

namespace NetWork.System
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