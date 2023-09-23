using System;
using FrameWork.ExtendFunctions;
using UnityEngine;
using UnityEngine.UI;

namespace FrameWork.ObjectPool.Dome
{
    public class ObjectPoolDome : MonoBehaviour
    {
        private ObjectPoolAsGameObject<AudioSource> _asGameObject;
        private void Start()
        {
            _asGameObject = new ObjectPoolAsGameObject<AudioSource>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                Debug.Log(_asGameObject.DeQueue().name);
            }
        }
    }
}