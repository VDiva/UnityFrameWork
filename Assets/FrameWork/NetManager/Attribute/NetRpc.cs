using System;

namespace FrameWork.NetManager.Attribute
{
    [AttributeUsage(AttributeTargets.Method,Inherited = false)]
    public class NetRpc: System.Attribute
    {
        
    }


    
    [AttributeUsage(AttributeTargets.Field)]
    public class NetFile : System.Attribute
    {
        private string _key;
        public NetFile(string key)
        {
            _key = key;
        }
    }
    
    
}