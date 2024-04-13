using System;
using UnityEngine;

namespace FrameWork
{
    public class csEvent : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                EventManager.AddListener(99,99,Events);
            }
            if (Input.GetKeyDown("2"))
            {
                EventManager.DispatchEvent(99,99);
            }
            if (Input.GetKeyDown("3"))
            {
                EventManager.RemoveListener(99,99,Events);
            }
        }

        public void Events(object[] cs)
        {
            MyLog.Log("aaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        }
        
    }
}