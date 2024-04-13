using System;
using UnityEngine;

namespace FrameWork
{
    public class cs : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                csEvent.AddListener(99,ss);
            }
            
            if (Input.GetKeyDown("2"))
            {
                csEvent.DispatchEvent(99);
            }
            
            if (Input.GetKeyDown("3"))
            {
                csEvent.RemoveListener(99,ss);
            }
        }

        public void ss(object[] s)
        {
            MyLog.Log("2222222");
        }
    }
}