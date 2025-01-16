using System;
using NetWorkClient;
using UnityEngine;

namespace FrameWork.Plugins.Net
{
    public class NetMrg : NetBehaviour
    {
        
        public static NetMrg Instance;
        public override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(this.gameObject);
            Instance = this;
            NetClient.RunTime = (() => Time.time);
        }

        
        private void Update()
        {
            NetClient.Update();
        }
    }
}