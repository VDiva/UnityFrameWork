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

        public string _abAnimName;
        public bool isRoot;
        protected LayerAnim[] _layerAnimArr;
        protected AnimationLayerMixerPlayable _layerMixerPlayable;
        
        protected AnimationPlayableOutput _output;

        protected PlayableGraph _playableGraph;

        protected bool _isInit;
        
        private ConcurrentDictionary<string, LayerAnim> _layerAnimDic;

        
        
        protected virtual void Awake()
        {
            GetComponent<Animator>().applyRootMotion = isRoot;
            _layerAnimDic = new ConcurrentDictionary<string, LayerAnim>();
            _isInit = false;
        }

        protected virtual void Start()
        {
            Init();
        }

        
        

        public virtual void Init()
        {
            if (_isInit)
            {
                _playableGraph.Destroy();
            }
            _layerAnimArr = new LayerAnim[animLayer.Length];
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

        public void SetSpeed(int layer,float speed)
        {
            if (_layerAnimArr!=null)
            {
                _layerAnimArr[layer].SetSpeed(speed);
            }
        }

        public void SetAnim(int layer,AnimationClip animationClip,float lerpSpeed=1,bool isSetTime=false)
        {
            _layerAnimDic.TryAdd(animationClip.name, anim);
            _layerAnimArr[layer].SetAnim(animationClip,lerpSpeed,isSetTime);
        }
        
        public void SetLayerWeight(int port,float weight)
        {
            _layerMixerPlayable.SetInputWeight(port,weight);
        }
        
        protected virtual void Update()
        {
            if (_layerAnimArr!=null)
            {
                for (int i = 0; i < _layerAnimArr.Length; i++)
                {
                    _layerAnimArr[i]?.Update();
                }
                GetCurAnimPlayLenght(0);
            }

            
        }

        public float GetCurAnimPlayLenght(int layer)
        {
            return _layerAnimArr[layer].GetCurAnimPlayLenght();
        }
        
        public bool IsGreater(int layer,float value)
        {
            return _layerAnimArr[layer].IsGreater(value);
        }
        
        public float GetCurAnimLerp(int layer)
        {
            return _layerAnimArr[layer].GetLerp();
        }
       
        private void OnDestroy()
        {
            _playableGraph.Destroy();
        }
    }
}