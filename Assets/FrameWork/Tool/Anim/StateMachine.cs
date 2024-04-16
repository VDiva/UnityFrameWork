
using UnityEngine;

namespace FrameWork
{
    public class StateMachine
    {
        private IAnim _anim;
        private AnimationController _animationController;
        private CharacterController _characterController;
        public StateMachine(AnimationController anim,CharacterController characterController)
        {
            _characterController = characterController;
            _animationController = anim;
        }
        
        public void RunAnim<T>() where T : IAnim,new()
        {
            if (_anim!=null)
            {
                _anim?.End((() =>
                {
                    _anim = new T();
                    _anim.Start(_animationController,_characterController,this);
                }));
            }
            else
            {
                _anim = new T();
                _anim.Start(_animationController,_characterController,this);
            }
        }


        public void Update()
        {
            _anim?.Update();
        }

    }
}