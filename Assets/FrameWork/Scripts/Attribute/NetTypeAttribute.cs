using System;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Method,AllowMultiple = true,Inherited = true)]
    public class NetTypeAttribute: Attribute
    {
        public Rpc Rpc;

        public NetTypeAttribute(Rpc rpc)
        {
            Rpc = rpc;
        }
    }
}