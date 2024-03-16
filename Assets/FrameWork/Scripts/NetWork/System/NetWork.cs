
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