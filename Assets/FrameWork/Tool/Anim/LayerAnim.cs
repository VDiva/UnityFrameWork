using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        
        public LayerAnim()
        {
            _layerAnims = new ConcurrentDictionary<string, int>();
        }
        
        public void Init(AnimLayerData animLayerData,PlayableGraph playableGraph,Action<string,int> init,Action<AnimationMixerPlayable> end)
        {
            _playableGraph = playableGraph;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, animLayerData.AnimData.Count);
            for (int j = 0; j < animLayerData.AnimData.Count; j++)
            {
                var animationClip = animLayerData.AnimData[j];
                var clip = AnimationClipPlayable.Create(_playableGraph,animationClip);
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
        }
        
        public void Update()
        {
            LerpAnim();
        }
        
        public void SetAnim(int port,float lerpSpeed=1)
        {
            if (port!=curMixProt)
            {
                lastMixProt = curMixProt;
                curMixProt = port;
                _mixlerp = 0;
                _lefpSpeed = lerpSpeed;
            }
            //InvokeRepeating("LerpAnim",0,0.02f);
        }
        
        private int _port;
        public void SetAnim(string animName,float lerpSpeed=1)
        {
            if (_layerAnims.TryGetValue(animName,out _port))
            {
                SetAnim(_port);
            }
        }
        
        private float _mixlerp;
        private void LerpAnim()
        {
            _mixlerp += Time.deltaTime*_lefpSpeed;
            _mixlerp = Mathf.Clamp(_mixlerp, 0, 1);
            if (lastMixProt!=-1)
            {
                _mixerPlayable.SetInputWeight(lastMixProt,1-_mixlerp);
            }

            if (curMixProt!=-1)
            {
                _mixerPlayable.SetInputWeight(curMixProt,_mixlerp);
            }
           
        }
    }
}