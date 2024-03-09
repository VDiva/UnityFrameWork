
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
            EventManager.Init();
        }

        private void FixedUpdate()
        {
            NetWorkSystem.UpdateMessage();
        }

    }
}