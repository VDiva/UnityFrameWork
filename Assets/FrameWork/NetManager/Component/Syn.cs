using UnityEngine;

namespace FrameWork.NetManager.Component
{
    [RequireComponent(typeof(Identity))]
    public class Syn : MonoBehaviour
    {
        protected Identity Identity;
        protected virtual void Awake()
        {
            Identity = GetComponent<Identity>();
        } 
    }
}