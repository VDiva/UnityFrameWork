
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork
{
    [RequireComponent(typeof(CharacterController))]
    public class CustomController : AnimationController
    {
        private CharacterController _characterController;
        private StateMachine _stateMachine;
        protected override void Awake()
        {
            base.Awake();
            _stateMachine = new StateMachine(this, _characterController);
            _characterController = GetComponent<CharacterController>();
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

        protected override void Update()
        {
            base.Update();
            _stateMachine?.Update();
            
            if (Input.GetKeyDown("1"))
            {
                _stateMachine.RunAnim<Idle>();
                MyLog.Log("按下1");
            }

            if (Input.GetKeyDown("2"))
            {
                _stateMachine.RunAnim<Run>();
                MyLog.Log("按下2");
            }
            
            if (Input.GetKeyDown("3"))
            {
                _stateMachine.RunAnim<RunEnd>();

            }
        }
    }
}