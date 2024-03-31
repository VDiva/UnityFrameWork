
using System;
using System.Reflection;
using NetWork.Type;
using Riptide;
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


        // private void OnApplicationQuit()
        // {
        //     //NetWorkSystem.DisConnect();
        //     NetWorkSystem.CloseGame();
        //     EventManager.Init();
        //     //Debug.Log("退出应用");
        // }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                NetWorkSystem.IsOnBackground = true;
            }
            
        }

        private void FixedUpdate()
        {
            NetWorkSystem.UpdateMessage();
        }
        
        
        

    }
}