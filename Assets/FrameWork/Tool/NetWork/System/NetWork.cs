
using System;
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


        private void OnEnable()
        {
            
        }

        private void OnDisable()
        {
            
        }


        private void OnApplicationQuit()
        {
            //NetWorkSystem.DisConnect();
            NetWorkSystem.CloseGame();
            EventManager.Init();
            //Debug.Log("退出应用");
        }

        private void FixedUpdate()
        {
            NetWorkSystem.UpdateMessage();
        }
        
        
        

    }
}