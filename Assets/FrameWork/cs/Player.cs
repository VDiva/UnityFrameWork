
using Riptide;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FrameWork
{
    public class Player : NetWorkSystemMono
    {
        protected  void Update()
        {

            if (!IsLocal)return;
            
            var h = Input.GetAxis("Horizontal");
            var v = Input.GetAxis("Vertical");

            Vector3 dir = new Vector3(h, 0, v);
            transform.Translate(dir*Time.deltaTime*5,Space.World);
                
            if (Input.GetKey(KeyCode.Space))
            {
                transform.Rotate(Vector3.one);
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                RpcMessage("CS",Rpc.All,new object[]{Random.Range(0,100)+"字"});
                //NetWorkSystem.Rpc();
            }

            
        }

        private void CS(object param)
        {
            var p = param as object[];
            TextManager.Instance.SetText(p[0].ToString());
        }
        
    }
}