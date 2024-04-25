
using UnityEngine;

namespace FrameWork
{
    public class StateMachine
    {
        private IAnim _anim;
        private AnimationController _animationController;
        private CharacterController _characterController;
        private MoveComponent _moveComponent;
        public StateMachine(AnimationController anim,CharacterController characterController,MoveComponent moveComponent)
        {
            _characterController = characterController;
            _animationController = anim;
            _moveComponent = moveComponent;
        }
        
        public void RunAnim<T>() where T : IAnim,new()
        {
            if (_anim!=null)
            {
                _anim?.End((() =>
                {
                    _anim= new T();
                    _anim.Start(_animationController, _characterController, this, _moveComponent);
                }));
            }
            else
            {
                _anim = new T();
                _anim.Start(_animationController,_characterController,this,_moveComponent);
            }
        }


        public void Update()
        {
            _anim?.Update();
        }

    }
}