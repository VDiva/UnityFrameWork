using System;
using FrameWork.ExtendFunctions;
using UnityEngine;
using UnityEngine.UI;

namespace FrameWork.ObjectPool.Dome
{
    public class ObjectPoolDome : MonoBehaviour
    {
        private ObjectPoolAsComponent<AudioSource> _asComponent;
        private void Start()
        {
            _asComponent = new ObjectPoolAsComponent<AudioSource>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                Debug.Log(_asComponent.DeQueue().name);
            }
        }
    }
}