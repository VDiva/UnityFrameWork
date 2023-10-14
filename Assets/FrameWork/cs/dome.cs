using FrameWork.NetManager.Attribute;
using UnityEngine;

namespace FrameWork.cs
{
    public class dome : MonoBehaviour
    {
        [NetRpc]
        public void Message()
        {
            Debug.Log("反射调用");
        }
    }
}