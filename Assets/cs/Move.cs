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

            if (Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.D))
            {
                _dir.x = Input.GetAxis("Horizontal");
              
            }
            
            if (Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.S))
            {
               
                _dir.y = Input.GetAxis("Vertical");
            }
            
            if (IsLocal)
            {
                
                transform.Translate(_dir*Time.deltaTime*5);
                transform.Rotate(_dir);
            }
        }
    }
}