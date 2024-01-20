using System;
using FrameWork.NetWork.Component;
using NetWork.System;
using NetWork.Type;
using Riptide;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FrameWork.cs
{
    public class Player : NetWorkSystemMono
    {
        private void Update()
        {
            if (IsLocal)
            {
                var h = Input.GetAxis("Horizontal");
                var v = Input.GetAxis("Vertical");
                
                
                
                Vector3 dir = new Vector3(h, 0, v);
                transform.Translate(dir*Time.deltaTime*5,Space.World);
                
                if (Input.GetKey(KeyCode.Space))
                {
                    transform.Rotate(Vector3.one);
                }

                if (Input.GetKeyDown(KeyCode.A))
                {
                    NetWorkSystem.Rpc("CS",this,Rpc.All,new object[]{Random.Range(0,100)+"字"});
                    //NetWorkSystem.Rpc();
                }
            }
        }


        private void CS(object param)
        {
            var p = param as object[];
            TextManager.Instance.SetText(p[0].ToString());
        }
        
    }
}