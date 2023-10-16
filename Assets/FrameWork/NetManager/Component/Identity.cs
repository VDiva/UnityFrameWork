using System;
using UnityEngine;

namespace FrameWork.NetManager.Component
{
    public class Identity : MonoBehaviour
    {
        public int id;
        public Identity()
        {
            NetManager.RequestAllocationId(this);
        }
        
        private void OnDestroy()
        {
            NetManager.Destroy(this);
        }
    }
}