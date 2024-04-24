namespace FrameWork
{
    public class Walk: AnimState
    {
        public override string AnimName()
        {
            return "walk";
        }


        public override void Update()
        {
            base.Update();
            if (MoveComponent.moveSpeed<=0.1f)
            {
                _stateMachine.RunAnim<Idle>();
            }

            if (MoveComponent.moveSpeed>10)
            {
                _stateMachine.RunAnim<Run>();
            }
        }
    }
}