using System;

namespace FrameWork
{
    public class Run: AnimState
    {
        private Action _action;
        private bool isEnd=false;
        public override string AnimName()
        {
            return "Run";
        }
        
    }
}