using NetWork.System;
using UnityEngine;

namespace FrameWork.NetWork.Component
{
    public class Identity : MonoBehaviour
    {
        public ushort _id;

        public void SetId(ushort id)
        {
            _id = id;
        }
        
        public ushort GetId()
        {
            return _id;
        }


        public bool IsLocal()
        {
            return _id == NetWorkSystem.GetClientId();
        }
    }
}