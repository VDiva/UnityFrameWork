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
            if (moveComponent.moveSpeed<=0.1f)
            {
                _stateMachine.RunAnim<Idle>();
            }else if (moveComponent.moveSpeed>1)
            {
                _stateMachine.RunAnim<Run>();
            }

            
        }
        
    }
}