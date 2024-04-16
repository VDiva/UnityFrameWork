using System;

namespace FrameWork
{
    public class RunEnd: AnimState
    {
        public override string AnimName()
        {
            return "RunEnd";
        }

        public override float Speed()
        {
            return -1;
        }
    }
}