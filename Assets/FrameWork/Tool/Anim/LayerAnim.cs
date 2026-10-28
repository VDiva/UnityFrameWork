using System;
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

        private int curMixProt=-1;

        private float _lefpSpeed=1;

        private AnimationMixerPlayable _mixerPlayable;

        private readonly Dictionary<string, int> _layerAnims;
        private readonly Dictionary<int, AnimationClipPlayable> _animationClipPlayables;
        private readonly List<float> _animLenghts;

        private float _curAnimPlayLenght;

        public LayerAnim()
        {
            _layerAnims = new Dictionary<string, int>();
            _animLenghts = new List<float>();
            _animationClipPlayables = new Dictionary<int, AnimationClipPlayable>();
        }

        public AnimationMixerPlayable GetMixer()
        {
            return _mixerPlayable;
        }

        public void Init(AnimLayerData animLayerData,PlayableGraph playableGraph,Action<string,int> init,Action<AnimationMixerPlayable> end)
        {
            _layerAnims.Clear();
            _animationClipPlayables.Clear();
            _animLenghts.Clear();
            lastMixProt = curMixProt = -1;
            _mixlerp = 1f;
            _curAnimPlayLenght = 0f;
            _playableGraph = playableGraph;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, (animLayerData.AnimData?.Count ?? 0));

            for (int j = 0; j < (animLayerData.AnimData?.Count ?? 0); j++)
            {
                var animationClip = animLayerData.AnimData[j];
                if (animationClip == null) throw new ArgumentException("动画层包含空 AnimationClip。");
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
            _layerAnims.Clear();
            _animationClipPlayables.Clear();
            _animLenghts.Clear();
            lastMixProt = curMixProt = -1;
            _mixlerp = 1f;
            _curAnimPlayLenght = 0f;
            _playableGraph = playableGraph;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, 0);

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

        private void SetAnim(int port,float lerpSpeed=1,bool isSetTime=false)
        {
            if (_animationClipPlayables.TryGetValue(port, out var clipPlayable))
            {
                if (isSetTime)
                {
                    clipPlayable.SetTime(0);
                }
                _curAnimPlayLenght = 0;
                if (curMixProt == port)
                {
                    if (lerpSpeed == -1)
                    {
                        clipPlayable.SetTime(0);
                        _mixlerp = 1f;
                        _lefpSpeed = 1f;
                        ApplyWeights();
                    }
                    return;
                }
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
                    int previousPort = lastMixProt;

                    _curAnimPlayLenght = _animLenghts[port] * _mixlerp;
                    _lefpSpeed = lerpSpeed;
                    lastMixProt = curMixProt;
                    curMixProt = port;
                    _mixlerp = 1 - _mixlerp;

                    if (previousPort!=-1 && previousPort!=port && _mixlerp!=1)
                    {
                        _mixerPlayable.SetInputWeight(previousPort,0);
                    }
                }

                if (lerpSpeed==-1)
                {
                    _mixlerp = 1;
                    _lefpSpeed = 1;
                    clipPlayable.SetTime(0);
                }
                ApplyWeights();
            }

        }

        public void SetAnim(AnimationClip animationClip,float lerpSpeed=1f,bool isSetTime=false)
        {
            if (animationClip == null) throw new ArgumentNullException(nameof(animationClip));
            if (!_layerAnims.TryGetValue(animationClip.name, out int port))
            {
                port = _animLenghts.Count;
                _mixerPlayable.SetInputCount(port + 1);
                var clip = AnimationClipPlayable.Create(_playableGraph, animationClip);
                if (!_playableGraph.Connect(clip, 0, _mixerPlayable, port))
                {
                    _playableGraph.DestroyPlayable(clip);
                    throw new InvalidOperationException("动画连接失败：" + animationClip.name);
                }
                _animLenghts.Add(animationClip.length);
                _animationClipPlayables.Add(port, clip);
                _layerAnims.Add(animationClip.name, port);
            }
            SetAnim(port, lerpSpeed, isSetTime);
        }

        public void SetAnim(string animName,float lerpSpeed=1,bool isSetTime=false)
        {
            if (_layerAnims.TryGetValue(animName,out int port))
            {
                SetAnim(port,lerpSpeed,isSetTime);

            }
        }

        private float _mixlerp=1;
        private void LerpAnim()
        {
            _curAnimPlayLenght += Time.deltaTime;
            if (_mixlerp >= 1f) return;
            _mixlerp = Mathf.Clamp01(_mixlerp + Time.deltaTime * _lefpSpeed);
            ApplyWeights();
        }

        private void ApplyWeights()
        {
            if (lastMixProt != -1 && lastMixProt != curMixProt)
                _mixerPlayable.SetInputWeight(lastMixProt, 1f - _mixlerp);
            if (curMixProt != -1) _mixerPlayable.SetInputWeight(curMixProt, _mixlerp);
        }

        public float GetLerp()
        {
            return _mixlerp;
        }

        public bool IsGreater(float value)
        {
            if (_animationClipPlayables.ContainsKey(curMixProt))
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
