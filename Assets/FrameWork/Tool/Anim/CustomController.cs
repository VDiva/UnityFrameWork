using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork
{
    public class CustomController : AnimationController
    {
        public override void Init()
        {
            ResetGraph();
            try
            {
                _playableGraph = PlayableGraph.Create(transform.root.name);
                _output = AnimationPlayableOutput.Create(_playableGraph, "AnimationControllerOut", GetComponent<Animator>());
                _layerAnimArr = new[] { new LayerAnim() };
                _layerAnimArr[0].Init(_playableGraph);
                _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _output.SetSourcePlayable(_layerAnimArr[0].GetMixer());
                _playableGraph.Play();
                _isInit = true;
            }
            catch { ResetGraph(); throw; }
        }
        public void SetSpeed(float speed) { SetSpeed(0, speed); }
    }
}
