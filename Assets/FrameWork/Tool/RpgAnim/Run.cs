using System;
using UnityEngine;

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
            if (moveComponent.moveSpeed<1)
            {
                if (Mathf.Abs(Input.GetAxisRaw("Horizontal"))>0.1f|| Mathf.Abs(Input.GetAxisRaw("Vertical"))>0.1f)
                {
                    _stateMachine.RunAnim<Walk>();
                }
                else
                {
                    _stateMachine.RunAnim<RunEnd>();
                }
               
            }

            
        }
        


    }
}