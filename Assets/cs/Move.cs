using System;
using FrameWork;
using UnityEngine;

namespace cs
{
    public class Move : NetWorkSystemMono
    {
        private void Update()
        {
            if (IsLocal)
            {
                var h = Input.GetAxis("Horizontal");
                var v = Input.GetAxis("Vertical");
                
                transform.Translate(new Vector3(h,0,v)*Time.deltaTime*5);
                
                
                transform.Rotate(new Vector3(h,v));

                if (Input.GetKeyDown(KeyCode.F))
                {
                   RpcMessage(cs);
                }
            }
        }


        [NetType(Rpc.All)]
        private void cs(object[] param)
        {
            Debug.Log("dddddddddddddddddddddddddddddddddddddddddd");
        }

    }
}