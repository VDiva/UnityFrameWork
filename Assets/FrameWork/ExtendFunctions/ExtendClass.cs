using UnityEngine;

namespace FrameWork
{
    public static class ExtendClass
    {
        public static void SetActive(this MonoBehaviour mono, bool active)
        {
            mono.gameObject.SetActive(active);
        }

        public static void Rpc(this GameObject gameObject,string methodName,Rpc rpc,object[] parma)
        {
            var net = gameObject.GetComponent<NetWorkSystemMono>();
            if (net!=null)
            {
                NetWorkSystem.Rpc(methodName,net,rpc,parma);
            }
        }
        
        public static void Rpc(this NetWorkSystemMono net,string methodName,Rpc rpc,object[] parma)
        {
            //var net = gameObject.GetComponent<NetWorkSystemMono>();
            if (net!=null)
            {
                NetWorkSystem.Rpc(methodName,net,rpc,parma);
            }
        }
    }
}