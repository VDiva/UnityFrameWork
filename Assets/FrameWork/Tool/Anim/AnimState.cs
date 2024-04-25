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
        protected MoveComponent moveComponent;
        
        
        public virtual void Start(AnimationController animationController, CharacterController characterController,StateMachine stateMachine,MoveComponent moveComponent)
        {
            _animationController = animationController;
            _characterController = characterController;
            _stateMachine = stateMachine;
            this.moveComponent = moveComponent;
            _animationClip = AssetBundlesLoad.LoadAsset<AnimationClip>(animationController._abAnimName, AnimName());
            animationController.SetAnim(0,_animationClip,Speed());
        }


        public virtual void Update()
        {
            
        }

        public virtual void End(Action end)
        {
            if (_animationController.IsGreater(0,AnimStrikes()))
            {
                end?.Invoke();
            }
        }

        public virtual string AnimName()
        {
            return "";
        }

        public virtual float AnimStrikes()
        {
            return _animationClip.length;
        }

        public virtual float Speed()
        {
            return 1;
        }
    }
}