using System;

namespace FrameWork.Attribute
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = true,Inherited = true)]
    public class UiModeAttribute: System.Attribute
    {
        public UiType UiType;
    }
}