using System;
using UnityEngine;

namespace FrameWork
{
    public class ActorMono : MonoBehaviour
    {
        private Actor _actor;


        public void SetActor(Actor actor)
        {
            _actor = actor;
        }

        private void Start()
        {
            if (_actor!=null)_actor.Start();
        }

        private void OnEnable()
        {
            if (_actor!=null)_actor.OnEnable();
        }

        private void OnDisable()
        {
            if (_actor!=null)_actor.OnDisable();
        }

        private void Update()
        {
            if (_actor!=null)_actor.Update();
        }

        private void FixedUpdate()
        {
            if (_actor!=null)_actor.FixedUpdate();
        }

        private void LateUpdate()
        {
            if (_actor!=null)_actor.LateUpdate();
        }

        private void OnDestroy()
        {
            if (_actor!=null)_actor.OnDestroy();
        }
    }
}