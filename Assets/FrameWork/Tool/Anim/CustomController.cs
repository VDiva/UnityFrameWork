
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(MoveComponent))]
    public class CustomController : AnimationController
    {
        private CharacterController _characterController;
        private StateMachine _stateMachine;
        private MoveComponent _moveComponent;
        protected override void Awake()
        {
            base.Awake();
            _moveComponent = GetComponent<MoveComponent>();
            _characterController = GetComponent<CharacterController>();
            _stateMachine = new StateMachine(this, _characterController,_moveComponent);
        }


        protected override void Start()
        {
            base.Start();
            _stateMachine.RunAnim<Idle>();
        }

        public override void Init()
        {
            
            if (_isInit)
            {
                _playableGraph.Destroy();
            }
            _isInit = true;
            _playableGraph=PlayableGraph.Create(transform.root.name);
            GraphVisualizerClient.Show(_playableGraph);
            _output=AnimationPlayableOutput.Create(_playableGraph,"AnimationControllerOut",GetComponent<Animator>());
            _layerAnimArr = new LayerAnim[1];
            _layerAnimArr[0] = new LayerAnim();
            _layerAnimArr[0].Init(_playableGraph);
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output.SetSourcePlayable(_layerAnimArr[0].GetMixer());
            _playableGraph.Play();
        }

        private void FixedUpdate()
        {
            _stateMachine?.Update();
        }
    }
}