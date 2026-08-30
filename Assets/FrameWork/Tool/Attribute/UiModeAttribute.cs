using System;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = true,Inherited = true)]
    public class UiModeAttribute: System.Attribute
    {
        public Mode Mode;
        

        public UiModeAttribute(Mode mode)
        {
            Mode = mode;
        }
    }
}