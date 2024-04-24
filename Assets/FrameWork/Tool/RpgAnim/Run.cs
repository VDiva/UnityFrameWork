using System;

namespace FrameWork
{
    public class Run: AnimState
    {
        private Action _action;
        private bool isEnd=false;
        public override string AnimName()
        {
            return "run";
        }

        public override void Update()
        {
            base.Update();

            if (MoveComponent.moveSpeed<=0.5f)
            {
                _stateMachine.RunAnim<RunEnd>();
            }else if (MoveComponent.moveSpeed<5)
            {
                _stateMachine.RunAnim<Walk>();
            }

            
        }
    }
}