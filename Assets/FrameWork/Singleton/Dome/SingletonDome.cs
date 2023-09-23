using System;
using UnityEngine;

namespace FrameWork.Singleton.Dome
{
    public class SingletonDome : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log(SingletonDomeClass.Instance.GetType().Name);
            Debug.Log(SingletonDomeMono.Instance.name);
        }
    }
}