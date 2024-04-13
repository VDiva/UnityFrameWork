using System;
using UnityEngine;

namespace FrameWork
{
    public class Move : NetWorkSystemMono
    {
        public float speed = 10;

        public Vector3 dir;


        private void Start()
        {
            if (!IsLocal)return;
            //csEvent.AddListener(99,1,(objects => { dir.x = (float)objects[0];} ));
        }

        private void Update()
        {
            if (!IsLocal)return;
            if (Application.platform==RuntimePlatform.WindowsPlayer|| Application.platform==RuntimePlatform.WindowsEditor)
            {
                dir.x = Input.GetAxis("Horizontal");
                dir.y = Input.GetAxis("Vertical");
            }
            
            transform.Translate(dir*speed*Time.deltaTime,Space.World);
        }
    }
}