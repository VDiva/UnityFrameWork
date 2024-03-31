using UnityEngine;

namespace FrameWork
{
    public partial class InputCs
    {
        public override void Start()
        {
            base.Start();
            Buttonleft.onClick.AddListener((() =>
            {
                Dispatch(99,1,new object[]{1.0f});
            }));
            
            Buttonright.onClick.AddListener((() =>
            {
                Dispatch(99,1,new object[]{-1.0f});
            }));
            
            Buttonting.onClick.AddListener((() =>
            {
                Dispatch(99,1,new object[]{0f});
            }));
            
            Buttonspanw.onClick.AddListener((() =>
            {
                NetWorkSystem.Instantiate<CsSphere>(Vector3.zero,Vector3.zero,true);
                MyLog.Log("点击生成");
            }));
            
            Buttonmatc.onClick.AddListener((() =>
            {
                NetWorkSystem.MatchingRoom("1",10);
                MyLog.Log("点击匹配");
            }));
            
        }
    }
}