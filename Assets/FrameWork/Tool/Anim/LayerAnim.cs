using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork
{
    public class LayerAnim
    {
        private PlayableGraph _playableGraph;
        
        private int lastMixProt = -1;
        private int lastLayerProt = -1;
        
        private int curMixProt=-1;

        private int curLayerProt = -1;

        private float _lefpSpeed=1;

        private AnimationMixerPlayable _mixerPlayable;
        
        private ConcurrentDictionary<string, int> _layerAnims;
        private ConcurrentDictionary<int, AnimationClipPlayable> _animationClipPlayables;
        private List<float> _animLenghts;
        private bool _isEnd=true;

        private StringBuilder _curAnim;

        private float _curAnimPlayLenght;

        private int _animIndex;
        public LayerAnim()
        {
            _curAnim = new StringBuilder();
            _layerAnims = new ConcurrentDictionary<string, int>();
            _animLenghts = new List<float>();
            _animationClipPlayables = new ConcurrentDictionary<int, AnimationClipPlayable>();
        }

        public AnimationMixerPlayable GetMixer()
        {
            return _mixerPlayable;
        }
        
        public void Init(AnimLayerData animLayerData,PlayableGraph playableGraph,Action<string,int> init,Action<AnimationMixerPlayable> end)
        {
            _playableGraph = playableGraph;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, animLayerData.AnimData.Count);
            
            for (int j = 0; j < animLayerData.AnimData.Count; j++)
            {
                var animationClip = animLayerData.AnimData[j];
                _animLenghts.Add(animationClip.length);
                var clip = AnimationClipPlayable.Create(_playableGraph,animationClip);
                _animationClipPlayables.TryAdd(j, clip);
                init?.Invoke(animationClip.name,j);
                _layerAnims.TryAdd(animationClip.name,j);
                _playableGraph.Connect(clip, 0, _mixerPlayable, j);
                if (j==0)
                {
                    _mixerPlayable.SetInputWeight(0,1);
                }
            }
            end?.Invoke(_mixerPlayable);
            
            
            if (_mixerPlayable.GetInputCount()>0)
            {
                _mixerPlayable.SetInputWeight(0,1);
                lastMixProt = 0;
                curMixProt = 0;
                _mixlerp = 1;
            }

            _curAnimPlayLenght = 0;
            _isEnd = true;
        }
        
        public void Init(PlayableGraph playableGraph)
        {
            _playableGraph = playableGraph;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, 1);
            //_mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, _animIndex);
            _isEnd = true;
            _curAnimPlayLenght = 0;
        }
        
        
        public void Update()
        {
            LerpAnim();
        }

        private AnimationClipPlayable _clipPlayable;
        private void SetAnim(int port,float lerpSpeed=1)
        {

            if (_mixlerp>=1)
            {
                
                if (_animationClipPlayables.TryGetValue(port,out _clipPlayable))
                {
                    _clipPlayable.SetTime(0);
                }
                _mixlerp = 0;
                _lefpSpeed = lerpSpeed;
                if (curMixProt!=-1)
                {
                    lastMixProt = curMixProt;
                }
                else
                {
                    _mixlerp = 1;
                }
                
                if (lerpSpeed==-1)
                {
                    _mixlerp = 1;
                    _lefpSpeed = 1;
                }
            
            
            
                _curAnimPlayLenght = 0;
                curMixProt = port; 
            }
            else
            {
                if (_animationClipPlayables.TryGetValue(port,out _clipPlayable))
                {
                    if (lastMixProt!=-1)
                    {
                        _mixerPlayable.SetInputWeight(lastMixProt,0);
                    }
                    _clipPlayable.SetTime(_animLenghts[port]*_mixlerp);
                    _curAnimPlayLenght = _animLenghts[port] * _mixlerp;
                    _lefpSpeed = lerpSpeed;
                    lastMixProt = curMixProt;
                    curMixProt = port;
                    _mixlerp = 0;
                }
            }
            
            
            
            //InvokeRepeating("LerpAnim",0,0.02f);
        }

        public void SetAnim(AnimationClip animationClip,float lerpSpeed=1f)
        {
            int curInput = _mixerPlayable.GetInputCount();
            if (_layerAnims.TryAdd(animationClip.name,_layerAnims.Count))
            {
                _animLenghts.Add(animationClip.length);
                var clip = AnimationClipPlayable.Create(_playableGraph,animationClip);
                _playableGraph.Connect(clip, 0, _mixerPlayable, _layerAnims.Count-1);
                _mixerPlayable.SetInputCount(curInput+1);
                _animationClipPlayables.TryAdd(_layerAnims.Count-1, clip);
                SetAnim(_layerAnims.Count-1,lerpSpeed);
                
            }
            else
            {
                SetAnim(_layerAnims[animationClip.name],lerpSpeed);
            }
        }
        
        
        private int _port;
        public void SetAnim(string animName,float lerpSpeed=1)
        {
            if (_layerAnims.TryGetValue(animName,out _port))
            {
                SetAnim(_port);
                _curAnim.Clear();
                _curAnim.Append(animName);
               // EventManager.DispatchEvent((int)MessageType.Animation,(int)AnimMessageType.Start,new object[]{animName});
                _isEnd = false;
            }
        }
        
        private float _mixlerp=1;
        private void LerpAnim()
        {
            _mixlerp += Time.deltaTime*_lefpSpeed;
            _mixlerp = Mathf.Clamp(_mixlerp, 0, 1);
            _curAnimPlayLenght += Time.deltaTime;
            if (lastMixProt!=-1)
            {
                _mixerPlayable.SetInputWeight(lastMixProt,1-_mixlerp);
            }

            if (curMixProt!=-1)
            {
                _mixerPlayable.SetInputWeight(curMixProt,_mixlerp);
            }

            if (!_isEnd&& _mixlerp>=1)
            {
                _isEnd = true;
                //EventManager.DispatchEvent((int)MessageType.Animation,(int)AnimMessageType.End,new object[]{_curAnim.ToString()});
            }
        }


        public float GetLerp()
        {
            return _mixlerp;
        }

        public bool IsGreater(float value)
        {
            return _curAnimPlayLenght >=value ;
        }
        
        public float GetCurAnimPlayLenght()
        {
            return _curAnimPlayLenght;
        }
        
    }
}