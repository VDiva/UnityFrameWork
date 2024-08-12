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


        private StringBuilder _curAnim;

        private float _curAnimPlayLenght;

        private AnimationClipPlayable _curPlayable;
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
          
        }
        
        public void Init(PlayableGraph playableGraph)
        {
            _playableGraph = playableGraph;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, 1);
            //_mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, _animIndex);
           
            _curAnimPlayLenght = 0;
        }
        
        
        public void SetSpeed(float speed)
        {
            _mixerPlayable.SetSpeed(speed);
        }
        
        public void Update()
        {
            LerpAnim();
            
        }
        
        private AnimationClipPlayable _clipPlayable;
        private int _prot = -1;
        private void SetAnim(int port,float lerpSpeed=1,bool isSetTime=false)
        {
            if (_animationClipPlayables.TryGetValue(port, out _clipPlayable))
            {
                if (isSetTime)
                {
                    _clipPlayable.SetTime(0);
                }
                _curAnimPlayLenght = 0;
                if (_mixlerp>=1)
                {
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
                
                    
                    
                    curMixProt = port; 
                }
                else
                {
                    _prot = lastMixProt;
                    
                    
                    _curAnimPlayLenght = _animLenghts[port] * _mixlerp;
                    _lefpSpeed = lerpSpeed;
                    lastMixProt = curMixProt;
                    curMixProt = port;
                    _mixlerp = 1 - _mixlerp;
                    
                    
                    if (_prot!=-1&& _prot!=port&& _mixlerp!=1)
                    {
                        _mixerPlayable.SetInputWeight(_prot,0);
                    }
                }
                
                if (lerpSpeed==-1)
                {
                    _mixlerp = 1;
                    _lefpSpeed = 1;
                    _clipPlayable.SetTime(0);
                }
            }

            
            
            
            
            //InvokeRepeating("LerpAnim",0,0.02f);
        }

        public void SetAnim(AnimationClip animationClip,float lerpSpeed=1f,bool isSetTime=false)
        {
            int curInput = _mixerPlayable.GetInputCount();
            if (_layerAnims.TryAdd(animationClip.name,_layerAnims.Count))
            {
                _animLenghts.Add(animationClip.length);
                var clip = AnimationClipPlayable.Create(_playableGraph,animationClip);
                _playableGraph.Connect(clip, 0, _mixerPlayable, _layerAnims.Count-1);
                _mixerPlayable.SetInputCount(curInput+1);
                _curPlayable = clip;
                _animationClipPlayables.TryAdd(_layerAnims.Count-1, clip);
                SetAnim(_layerAnims.Count-1,lerpSpeed,isSetTime);
                
            }
            else
            {
                SetAnim(_layerAnims[animationClip.name],lerpSpeed,isSetTime);
            }
        }
        
        
        private int _port;
        public void SetAnim(string animName,float lerpSpeed=1,bool isSetTime=false)
        {
            if (_layerAnims.TryGetValue(animName,out _port))
            {
                SetAnim(_port,lerpSpeed,isSetTime);
                _curAnim.Clear();
                _curAnim.Append(animName);
               // EventManager.DispatchEvent((int)MessageType.Animation,(int)AnimMessageType.Start,new object[]{animName});
               
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
            
        }


        public float GetLerp()
        {
            return _mixlerp;
        }

        public bool IsGreater(float value)
        {
            if (_animationClipPlayables.TryGetValue(curMixProt,out var clipPlayable))
            {
                return _curAnimPlayLenght >=value ;
            }
            else
            {
                return true;
            }
        }
        
        public float GetCurAnimPlayLenght()
        {
            return _curAnimPlayLenght;
        }
        
    }
}