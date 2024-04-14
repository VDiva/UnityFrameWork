using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork
{
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour
    {
        public AnimLayerData[] animLayer;

        private LayerAnim[] _layerAnimArr;
        private AnimationLayerMixerPlayable _layerMixerPlayable;
        
        private AnimationPlayableOutput _output;

        private PlayableGraph _playableGraph;

        private bool _isInit;
        
        private ConcurrentDictionary<string, LayerAnim> _layerAnimDic;
        private void Awake()
        {
            _layerAnimArr = new LayerAnim[animLayer.Length];
            _layerAnimDic = new ConcurrentDictionary<string, LayerAnim>();
            _isInit = false;
        }

        private void Start()
        {
            Init();
        }


        public void Init()
        {
            if (_isInit)
            {
                _playableGraph.Destroy();
            }
            
            _playableGraph=PlayableGraph.Create(transform.root.name);
            _isInit = true;
            GraphVisualizerClient.Show(_playableGraph);
            _layerMixerPlayable=AnimationLayerMixerPlayable.Create(_playableGraph,animLayer.Length);
            _output=AnimationPlayableOutput.Create(_playableGraph,"AnimationControllerOut",GetComponent<Animator>());
            
            for (int i = 0; i < animLayer.Length; i++)
            {
                var item = animLayer[i];
                LayerAnim conItem=new LayerAnim();
                conItem.Init(item,_playableGraph,((clipName, port) =>
                {
                    _layerAnimDic.TryAdd(clipName,conItem);
                }),(playable =>
                {
                    _playableGraph.Connect(playable, 0, _layerMixerPlayable, i);
                } ));

                _layerAnimArr[i] = conItem;
                if (item.AvatarMask!=null)
                {
                    _layerMixerPlayable.SetLayerMaskFromAvatarMask((uint)i,item.AvatarMask);
                }
                _layerMixerPlayable.SetInputWeight(i,1);
            }
            
            _output.SetSourcePlayable(_layerMixerPlayable);
            
            _playableGraph.Play();
        }
        
        
        private LayerAnim anim;
        public void SetAnim(string animName,float lerpSpeed=1)
        {
            if (_layerAnimDic.TryGetValue(animName,out anim))
            {
                anim.SetAnim(animName,lerpSpeed);
            }
        }
        
        public void SetAnim(int index,int port,float lerpSpeed=1)
        {
            if (_layerAnimArr.Length > port)
            {
                _layerAnimArr[index].SetAnim(port,lerpSpeed);
            }
        }
        
        public void SetLayerWeight(int port,float weight)
        {
            _layerMixerPlayable.SetInputWeight(port,weight);
        }
        
        private void Update()
        {
            for (int i = 0; i < _layerAnimArr.Length; i++)
            {
                _layerAnimArr[i]?.Update();
            }
        }


       
        private void OnDestroy()
        {
            _playableGraph.Destroy();
        }
    }
}