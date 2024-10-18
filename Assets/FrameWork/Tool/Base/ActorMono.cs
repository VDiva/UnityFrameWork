
using UnityEngine;

namespace FrameWork
{
    public class ActorMono : MonoBehaviour
    {
        private Actor _actor;
        public void SetActor(Actor actor) { _actor = actor; }
        public int GetIndex() { return _actor.GetIndex(); }
        //public ushort GetObjId() { return _actor.GetIdentity().GetObjId(); }
        //public ushort GetClientId() { return _actor.GetIdentity().GetClientId(); }
        private void Awake() { _actor?.Awake(); }
        private void Start() { _actor?.Start(); }
        private void OnEnable() { _actor?.OnEnable(); }
        private void OnDisable() { _actor?.OnDisable(); }
        private void Update() { _actor?.Update(Time.deltaTime); }
        private void FixedUpdate() { _actor?.FixedUpdate(Time.fixedDeltaTime); }
        private void LateUpdate() { _actor?.LateUpdate(); }
        private void OnDestroy() { _actor?.OnDestroy(); }
    }
}