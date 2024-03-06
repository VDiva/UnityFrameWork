using System;
using FrameWork;
using UnityEngine;
using Random = UnityEngine.Random;

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
                   RpcMessage(cs,new object[]{"随机到"+Random.Range(1,1000)});
                }
                
                if (Input.GetKeyDown(KeyCode.G))
                {
                    NetWorkSystem.Destroy(this);
                }
                
            }
        }


        [NetType(Rpc.All)]
        private void cs(object[] param)
        {
            FindObjectOfType<CsText>().SetText(param[0] as string);
        }

    }
}