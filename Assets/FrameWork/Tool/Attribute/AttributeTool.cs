using System;
using UnityEngine;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LogAttribute : Attribute{}
    
    [AttributeUsage(AttributeTargets.Method)]
    public class NetToServerAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Method)]
    public class NetToClientAttribute : Attribute
    {
        public NetToClientAttribute()
        {
            Debug.Log("aaaaa");
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class NetSyncValueAttribute : Attribute
    {
        public NetSyncValueAttribute()
        {
            Debug.Log("aaaaa");
        }
    }
}