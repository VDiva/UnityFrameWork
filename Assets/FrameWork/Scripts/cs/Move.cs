using System;
using UnityEngine;

namespace FrameWork
{
    public class Move : NetWorkSystemMono
    {
        public float speed = 10;
        private void Update()
        {
            if (!IsLocal)return;
            var h = Input.GetAxis("Horizontal");
            var v = Input.GetAxis("Vertical");
            transform.Translate(new Vector3(h,v)*speed*Time.deltaTime,Space.World);
        }
    }
}