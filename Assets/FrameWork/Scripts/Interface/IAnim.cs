using System;

namespace FrameWork
{
    public interface IAnim
    {
        void Start();
        void Update();
        void End(Action end);
    }
}