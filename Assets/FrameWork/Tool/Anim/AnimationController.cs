using System;
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
        private readonly Dictionary<string, LayerAnim> _layerAnimDic = new Dictionary<string, LayerAnim>();

        protected virtual void Awake() { GetComponent<Animator>().applyRootMotion = isRoot; }
        protected virtual void Start() { Init(); }

        protected void ResetGraph()
        {
            _isInit = false;
            _layerAnimDic.Clear();
            _layerAnimArr = null;
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
            _layerMixerPlayable = default;
        }

        public virtual void Init()
        {
            ResetGraph();
            try
            {
                int count = animLayer?.Length ?? 0;
                _layerAnimArr = new LayerAnim[count];
                _playableGraph = PlayableGraph.Create(transform.root.name);
                _layerMixerPlayable = AnimationLayerMixerPlayable.Create(_playableGraph, count);
                _output = AnimationPlayableOutput.Create(_playableGraph, "AnimationControllerOut", GetComponent<Animator>());
                for (int i = 0; i < count; i++)
                {
                    int layer = i;
                    var item = animLayer[i];
                    var controller = new LayerAnim();
                    controller.Init(item, _playableGraph,
                        (name, port) => { if (!_layerAnimDic.ContainsKey(name)) _layerAnimDic.Add(name, controller); },
                        playable => _playableGraph.Connect(playable, 0, _layerMixerPlayable, layer));
                    _layerAnimArr[i] = controller;
                    if (item.AvatarMask != null) _layerMixerPlayable.SetLayerMaskFromAvatarMask((uint)i, item.AvatarMask);
                    _layerMixerPlayable.SetInputWeight(i, 1);
                }
                _output.SetSourcePlayable(_layerMixerPlayable);
                _playableGraph.Play();
                _isInit = true;
            }
            catch { ResetGraph(); throw; }
        }

        public void SetAnim(string animName, float lerpSpeed = 1)
        {
            if (animName != null && _layerAnimDic.TryGetValue(animName, out var layer)) layer.SetAnim(animName, lerpSpeed);
        }

        protected LayerAnim GetLayer(int layer)
        {
            if (!_isInit || _layerAnimArr == null) throw new InvalidOperationException("请先初始化动画控制器。");
            if (layer < 0 || layer >= _layerAnimArr.Length) throw new ArgumentOutOfRangeException(nameof(layer));
            return _layerAnimArr[layer];
        }

        public void SetSpeed(int layer, float speed) { GetLayer(layer).SetSpeed(speed); }
        public void SetAnim(int layer, AnimationClip animationClip, float lerpSpeed = 1, bool isSetTime = false)
        {
            if (animationClip == null) throw new ArgumentNullException(nameof(animationClip));
            var target = GetLayer(layer);
            target.SetAnim(animationClip, lerpSpeed, isSetTime);
            _layerAnimDic[animationClip.name] = target;
        }
        public void SetLayerWeight(int port, float weight)
        {
            GetLayer(port);
            if (!_layerMixerPlayable.IsValid()) throw new InvalidOperationException("当前控制器没有分层混合器。");
            _layerMixerPlayable.SetInputWeight(port, weight);
        }
        protected virtual void Update()
        {
            if (!_isInit || _layerAnimArr == null) return;
            foreach (var layer in _layerAnimArr) layer?.Update();
        }
        public float GetCurAnimPlayLenght(int layer) => GetLayer(layer).GetCurAnimPlayLenght();
        public bool IsGreater(int layer, float value) => GetLayer(layer).IsGreater(value);
        public float GetCurAnimLerp(int layer) => GetLayer(layer).GetLerp();
        protected virtual void OnDestroy() { ResetGraph(); }
    }
}
