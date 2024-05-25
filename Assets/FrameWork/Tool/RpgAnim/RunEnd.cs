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
            return 5;
        }

        public override void Update()
        {
            base.Update();
            if (moveComponent.moveSpeed<=0.1f)
            {
                _stateMachine.RunAnim<Idle>();
            }else if (moveComponent.moveSpeed>1)
            {
                _stateMachine.RunAnim<Run>();
            }else if (moveComponent.moveSpeed>0.1f)
            {
                _stateMachine.RunAnim<Walk>();
            }
        }

        public override bool IsSetTime()
        {
            return true;
        }
    }
}