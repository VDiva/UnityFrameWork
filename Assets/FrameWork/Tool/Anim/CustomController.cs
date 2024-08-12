
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork
{
    public class CustomController : AnimationController
    {
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
        
        public void SetSpeed(float speed)
        {
            SetSpeed(0,speed);
        }
    }
}