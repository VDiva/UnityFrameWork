using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FrameWork
{
    public enum ActorType
    {
        Other,//默认 
        Player,//角色
        Monster,//怪物
        PoHuai//破坏物
    }
    
    public class Actor
    {
        //public string ActorName;
        private int Index=-999999;
        private GameObject _gameObject;

        //private Identity _identity;
        
        public Transform transform => _gameObject.transform;
        public bool isInit=>_gameObject!=null;
        public ActorType type;
        public string actorName;
        
        private readonly UniTaskCompletionSource<Actor> _initialization = new UniTaskCompletionSource<Actor>();
        private bool _spawnStarted;
        public UniTask<Actor> Initialization => _initialization.Task;

        public Actor() { _ = SpawnObject(null); }
        public Actor(Transform trans) { _ = SpawnObject(trans); }

        /// <summary>同一 Actor 只生成一次，可等待此任务获取初始化失败信息。</summary>
        public UniTask<Actor> SpawnObject(Transform trans)
        {
            if (!_spawnStarted)
            {
                _spawnStarted = true;
                SpawnCore(trans).Forget(Debug.LogException);
            }
            return Initialization;
        }

        private async UniTask SpawnCore(Transform parent)
        {
            GameObject instance = null;
            try
            {
                // 构造函数返回后再调用派生类生命周期。
                await UniTask.Yield(PlayerLoopTiming.Update);
                var info = GetType().GetCustomAttribute<ActorInfoAttribute>();
                if (info == null || string.IsNullOrWhiteSpace(info.PrefabName))
                    throw new InvalidOperationException(GetType().Name + " 缺少有效的 ActorInfoAttribute");
                instance = await ABMrg.InstantiateAsync(info.PrefabName);
                instance.SetActive(false);
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                _gameObject = instance;
                var actorMono = instance.AddComponent<ActorMono>();
                actorMono.SetActor(this);
                Awake();
                instance.SetActive(true);
                Start();
                if (parent && this is UiActor ui)
                {
                    ui.ResetRect();
                    ui.OpenAnim();
                }
                _initialization.TrySetResult(this);
            }
            catch (Exception error)
            {
                _gameObject = null;
                if (instance != null) ABMrg.ReleaseInstantiate(instance);
                _initialization.TrySetException(error);
                Debug.LogException(error);
            }
        }

        //public Identity GetIdentity() { return _identity; }
        
        
        public GameObject GetGameObject() { return _gameObject; }

        public T AddComponent<T>() where T: Component { return _gameObject.AddComponent<T>(); }
        
        
        public int GetIndex() { return Index; }
        
        public void SetIndex(int index) { Index = index; }
        
        public virtual void Awake() {}

        public virtual void Start() {}

        public virtual void OnEnable() {}

        public virtual void OnDisable() {}

        public virtual void Update(float deltaTime) {}

        public virtual void FixedUpdate(float deltaTime) {}

        public virtual void LateUpdate() {}

        public virtual void OnDestroy() {}

        public virtual void OnTriggerEnter() {}
        public virtual void OnTriggerEnter2D() {}
        public virtual void OnTriggerExit2D() {}
        
        public virtual void OnCollisionEnter() {}
        
    }
}