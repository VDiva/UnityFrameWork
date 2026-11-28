using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    /// <summary>基础 Actor 宿主：通过集中式 ActorTickManager 驱动生命周期，避免为每个 Actor 挂载多个 Driver 组件。</summary>
    public class ActorMono : MonoBehaviour
    {
        private Actor _actor;
        public Actor GetActor => _actor;
        public void SetActor(Actor actor) { _actor = actor; }
        public int GetIndex() { return _actor.GetIndex(); }

        private void OnEnable()
        {
            if (_actor == null) return;
            ActorTickManager.Register(_actor);
            _actor.OnEnable();
        }

        private void OnDisable()
        {
            if (_actor == null) return;
            ActorTickManager.Unregister(_actor);
            _actor.OnDisable();
        }

        private void OnDestroy()
        {
            if (_actor == null) return;
            ActorTickManager.Unregister(_actor);
            _actor.OnDestroy();
        }
    }

    /// <summary>集中驱动所有覆写了 Update/FixedUpdate/LateUpdate 的活动 Actor，采用 Swap-Back O(1) 注册与注销。</summary>
    [AddComponentMenu("")]
    internal sealed class ActorTickManager : MonoBehaviour
    {
        private static ActorTickManager _instance;
        private static bool _isQuitting;
        private static readonly List<Actor> UpdateList = new List<Actor>(64);
        private static readonly List<Actor> FixedUpdateList = new List<Actor>(32);
        private static readonly List<Actor> LateUpdateList = new List<Actor>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
            UpdateList.Clear();
            FixedUpdateList.Clear();
            LateUpdateList.Clear();
        }

        private static void EnsureInstance()
        {
            if (_instance != null || _isQuitting || !Application.isPlaying) return;
            var go = new GameObject("[ActorTickManager]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ActorTickManager>();
        }

        internal static void Register(Actor actor)
        {
            if (actor == null) return;
            bool needDriver = false;
            if (actor.HasUpdate && actor.UpdateTickIndex < 0)
            {
                actor.UpdateTickIndex = UpdateList.Count;
                UpdateList.Add(actor);
                needDriver = true;
            }
            if (actor.HasFixedUpdate && actor.FixedUpdateTickIndex < 0)
            {
                actor.FixedUpdateTickIndex = FixedUpdateList.Count;
                FixedUpdateList.Add(actor);
                needDriver = true;
            }
            if (actor.HasLateUpdate && actor.LateUpdateTickIndex < 0)
            {
                actor.LateUpdateTickIndex = LateUpdateList.Count;
                LateUpdateList.Add(actor);
                needDriver = true;
            }
            if (needDriver)
            {
                EnsureInstance();
                if (_instance != null && !_instance.enabled) _instance.enabled = true;
            }
        }

        internal static void Unregister(Actor actor)
        {
            if (actor == null) return;
            if (actor.UpdateTickIndex >= 0)
            {
                RemoveSwapBack(UpdateList, actor.UpdateTickIndex, (a, idx) => a.UpdateTickIndex = idx);
                actor.UpdateTickIndex = -1;
            }
            if (actor.FixedUpdateTickIndex >= 0)
            {
                RemoveSwapBack(FixedUpdateList, actor.FixedUpdateTickIndex, (a, idx) => a.FixedUpdateTickIndex = idx);
                actor.FixedUpdateTickIndex = -1;
            }
            if (actor.LateUpdateTickIndex >= 0)
            {
                RemoveSwapBack(LateUpdateList, actor.LateUpdateTickIndex, (a, idx) => a.LateUpdateTickIndex = idx);
                actor.LateUpdateTickIndex = -1;
            }
            if (_instance != null && UpdateList.Count == 0 && FixedUpdateList.Count == 0 && LateUpdateList.Count == 0)
                _instance.enabled = false;
        }

        private static void RemoveSwapBack(List<Actor> list, int index, Action<Actor, int> setIndex)
        {
            int last = list.Count - 1;
            if (index < 0 || index > last) return;
            if (index != last)
            {
                var moved = list[last];
                list[index] = moved;
                if (moved != null) setIndex(moved, index);
            }
            list.RemoveAt(last);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = UpdateList.Count - 1; i >= 0; i--)
            {
                if (i >= UpdateList.Count) continue;
                var actor = UpdateList[i];
                try { actor?.Update(dt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            for (int i = FixedUpdateList.Count - 1; i >= 0; i--)
            {
                if (i >= FixedUpdateList.Count) continue;
                var actor = FixedUpdateList[i];
                try { actor?.FixedUpdate(dt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private void LateUpdate()
        {
            for (int i = LateUpdateList.Count - 1; i >= 0; i--)
            {
                if (i >= LateUpdateList.Count) continue;
                var actor = LateUpdateList[i];
                try { actor?.LateUpdate(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private void OnApplicationQuit() => _isQuitting = true;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
