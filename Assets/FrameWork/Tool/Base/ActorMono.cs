using UnityEngine;

namespace FrameWork
{
    /// <summary>基础 Actor 宿主：不包含空 Update/FixedUpdate/LateUpdate，避免无回调 Actor/UI 每帧触发原生调用。</summary>
    public class ActorMono : MonoBehaviour
    {
        private Actor _actor;
        public Actor GetActor => _actor;
        public void SetActor(Actor actor) { _actor = actor; }
        public int GetIndex() { return _actor.GetIndex(); }
        private void OnEnable() { _actor?.OnEnable(); }
        private void OnDisable() { _actor?.OnDisable(); }
        private void OnDestroy() { _actor?.OnDestroy(); }
    }

    [AddComponentMenu("")]
    internal sealed class ActorUpdateDriver : MonoBehaviour
    {
        private Actor _actor;
        internal void Bind(Actor actor) => _actor = actor;
        private void Update() => _actor?.Update(Time.deltaTime);
    }

    [AddComponentMenu("")]
    internal sealed class ActorFixedUpdateDriver : MonoBehaviour
    {
        private Actor _actor;
        internal void Bind(Actor actor) => _actor = actor;
        private void FixedUpdate() => _actor?.FixedUpdate(Time.fixedDeltaTime);
    }

    [AddComponentMenu("")]
    internal sealed class ActorLateUpdateDriver : MonoBehaviour
    {
        private Actor _actor;
        internal void Bind(Actor actor) => _actor = actor;
        private void LateUpdate() => _actor?.LateUpdate();
    }
}