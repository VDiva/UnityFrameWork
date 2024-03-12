using System;
using FrameWork;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

namespace cs
{
    public class Move : NetWorkSystemMono
    {
        private Vector3 _dir=Vector3.zero;
        private void Start()
        {
            if (!IsLocal)return;
            EventManager.AddListener(5,1,(objects => { _dir.x = (float)objects[0];} ));
        }

        public void Update()
        {
            if (IsLocal)
            {
#if UNITY_EDITOR_WIN|| UNITY_WINDOWS
                _dir.x = Input.GetAxis("Horizontal");
                _dir.y = Input.GetAxis("Vertical");
#endif
                
                transform.Translate(_dir*Time.deltaTime*5);
                transform.Rotate(_dir);
            }
        }
    }
}