using System;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class UiModeAttribute : Attribute
    {
        public Mode Mode;

        /// <summary>为 true 时隐藏后不会自动销毁回收（如常驻主界面）。</summary>
        public bool Permanent;

        /// <summary>
        /// 隐藏后超过该秒数未再次打开则自动销毁并卸载内存；
        /// 小于 0 表示使用 <see cref="UiManager.DefaultAutoDestroyDelaySeconds"/>，等于 0 表示隐藏时立即销毁。
        /// </summary>
        public float AutoDestroyDelaySeconds = -1f;

        public UiModeAttribute(Mode mode)
        {
            Mode = mode;
        }
    }
}
