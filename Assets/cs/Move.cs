using System;
using FrameWork;
using UnityEngine;
using Random = UnityEngine.Random;

namespace cs
{
    public class Move : NetWorkSystemMono
    {
        public void Update()
        {
            if (IsLocal)
            {
                var h = Input.GetAxis("Horizontal");
                var v = Input.GetAxis("Vertical");
                transform.Translate(new Vector3(h,0,v)*Time.deltaTime*5);
                transform.Rotate(new Vector3(h,v));
            }
        }
    }
}