using System;

namespace FrameWork
{
    public class RunEnd: AnimState
    {
        public override string AnimName()
        {
            return "runEnd";
        }
        
        public override float Speed()
        {
            return 10;
        }


        public override void Update()
        {
            base.Update();
            if (_animationController.GetCurAnimPlayLenght(0)>=_animationClip.length)
            {
                _stateMachine.RunAnim<Idle>();
            }
        }
    }
}