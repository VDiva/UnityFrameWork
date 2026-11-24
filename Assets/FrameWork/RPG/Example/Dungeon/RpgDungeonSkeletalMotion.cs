using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FrameWork.FPS
{
    internal sealed class RpgDungeonSkeletalMotion : IDisposable
    {
        private const int Idle = 0;
        private const int Walk = 1;
        private const int Sprint = 2;
        private const int Fire = 3;
        private const int Melee = 4;
        private const int Reload = 5;
        private const int Defeat = 6;
        private const int Count = 7;

        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] motions = new AnimationClipPlayable[Count];
        private readonly float[] lengths = new float[Count];

        private RpgDungeonSkeletalMotion(Animator animator, AnimationClip[] clips)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create(animator.gameObject.name + " locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, Count);
            for (int i = 0; i < Count; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                var motion = AnimationClipPlayable.Create(graph, clip);
                motion.SetApplyFootIK(false);
                motion.SetSpeed(0d);
                graph.Connect(motion, 0, mixer, i);
                motions[i] = motion;
                lengths[i] = clip.length;
            }
            AnimationPlayableOutput.Create(graph, "Character motion", animator).SetSourcePlayable(mixer);
            graph.Play();
        }

        public static RpgDungeonSkeletalMotion Create(Transform body, string resource)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(resource)) return null;

            var imported = System.Linq.Enumerable.OfType<AnimationClip>(
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath(RpgDungeonDressing.ArtPath + resource + ".fbx"));
            var clips = System.Linq.Enumerable.ToArray(imported);
            var idle = Find(clips, "idle");
            var walk = Find(clips, "walk");
            if (idle == null || walk == null) return null;

            var animator = body.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = body.GetChild(0).gameObject.AddComponent<Animator>();
            return new RpgDungeonSkeletalMotion(animator, new[]
            {
                idle, walk, Find(clips, "sprint") ?? walk,
                Find(clips, "holding-both-shoot"), Find(clips, "attack-melee-right"),
                Find(clips, "pick-up"), Find(clips, "die")
            });
#else
            return null;
#endif
        }

        public bool HasDefeat => lengths[Defeat] > 0f;

        public void Sample(float time, float speed, float movement, float fire, float melee, float reload, float defeat)
        {
            int action = -1;
            float progress = 0f;
            if (defeat >= 0f && HasDefeat) { action = Defeat; progress = defeat; }
            else if (reload >= 0f && lengths[Reload] > 0f) { action = Reload; progress = reload; }
            else if (melee >= 0f && lengths[Melee] > 0f) { action = Melee; progress = melee; }
            else if (fire >= 0f && lengths[Fire] > 0f) { action = Fire; progress = fire; }

            float actionWeight = action == Defeat ? 1f :
                action >= 0 ? Mathf.Min(1f, Mathf.Min(progress * 9f, (1f - progress) * 7f)) : 0f;
            float locomotion = 1f - actionWeight;
            float sprint = Mathf.InverseLerp(3.5f, 6f, speed);
            SetLoop(Idle, time, (1f - movement) * locomotion);
            SetLoop(Walk, time, movement * (1f - sprint) * locomotion);
            SetLoop(Sprint, time, movement * sprint * locomotion);
            for (int i = Fire; i < Count; i++)
            {
                if (lengths[i] <= 0f) continue;
                mixer.SetInputWeight(i, i == action ? actionWeight : 0f);
                if (i == action) motions[i].SetTime(lengths[i] * Mathf.Min(progress, 0.9999f));
            }
            graph.Evaluate(0f);
        }

        private void SetLoop(int input, float time, float weight)
        {
            mixer.SetInputWeight(input, weight);
            motions[input].SetTime(Mathf.Repeat(time, lengths[input]));
        }

        private static AnimationClip Find(AnimationClip[] clips, string name)
        {
            foreach (var clip in clips)
            {
                if (clip.length <= 0f || clip.legacy) continue;
                if (clip.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    clip.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
            return null;
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
