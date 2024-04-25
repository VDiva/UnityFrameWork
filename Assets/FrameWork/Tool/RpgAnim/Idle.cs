using System;
using UnityEngine;

namespace FrameWork
{
    public class Idle: AnimState
    {
        public override string AnimName()
        {
            return "Idle";
        }


        public override void Update()
        {
            base.Update();

            if (moveComponent.moveSpeed>0.1f)
            {
                _stateMachine.RunAnim<Walk>();
            }
        }

        public override float AnimStrikes()
        {
            return 0;
        }
    }
}