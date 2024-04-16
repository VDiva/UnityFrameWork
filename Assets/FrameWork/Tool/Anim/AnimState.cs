using System;
using UnityEngine;

namespace FrameWork
{
    public class AnimState: IAnim
    {
        protected AnimationController _animationController;
        protected CharacterController _characterController;
        protected AnimationClip _animationClip;
        protected StateMachine _stateMachine;
        public virtual void Start(AnimationController animationController, CharacterController characterController,StateMachine stateMachine)
        {
            if (animationController.IsGreater(0,AnimStrikes()))
            {
                _animationController = animationController;
                _characterController = characterController;
                _stateMachine = stateMachine;
                _animationClip = AssetBundlesLoad.LoadAsset<AnimationClip>(animationController._abAnimName, AnimName());
                animationController.SetAnim(0,_animationClip,Speed());
            }
        }

        public virtual void Update()
        {
            
        }

        public virtual void End(Action end)
        {
            end?.Invoke();
        }

        public virtual string AnimName()
        {
            return "";
        }

        public virtual float AnimStrikes()
        {
            return 0;
        }

        public virtual float Speed()
        {
            return 1;
        }
    }
}