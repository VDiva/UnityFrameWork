
using UnityEngine;

namespace FrameWork
{
    public class CsMove : NetWorkSystemMono
    {
        public float time;
        protected  void Update()
        {


            if (!IsLocal)return;
            
            time = Mathf.Sin(Time.time)*5;
            transform.Translate(new Vector3(time,0,0)*Time.deltaTime,Space.World);
            
        }
    }
}