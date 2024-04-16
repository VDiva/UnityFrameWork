using System;
using UnityEngine;

namespace FrameWork
{
    public interface IAnim
    {
        void Start(AnimationController animationController,CharacterController characterController,StateMachine stateMachine);
        void Update();
        void End(Action end);

        string AnimName();
        float AnimStrikes();

        float Speed();
    }
}