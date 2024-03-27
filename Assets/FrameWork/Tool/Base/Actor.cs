using System;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class Actor
    {
        //public string ActorName;
        private int Index=-999999;
        private GameObject _gameObject;

        private Identity _identity;
        
        protected Transform transform => _gameObject.transform;
        
        protected Actor()
        {
            var type=GetType();
            var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            if (infoAttribute==null||infoAttribute.PackName==""|| infoAttribute.PrefabName=="")return;
            var go=AssetBundlesLoad.LoadAsset<GameObject>(infoAttribute.PackName, infoAttribute.PrefabName);
            _gameObject = GameObject.Instantiate(go);
            _gameObject.SetActive(false);
            _identity = _gameObject.AddComponent<Identity>();
            var actorMono=_gameObject.AddComponent<ActorMono>();
            actorMono.SetActor(this);
            _gameObject.SetActive(true);
        }
       
        protected Actor(Transform trans)
        {
            var type=GetType();
            var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            if (infoAttribute==null||infoAttribute.PackName==""|| infoAttribute.PrefabName=="")return;
            var go=AssetBundlesLoad.LoadAsset<GameObject>(infoAttribute.PackName, infoAttribute.PrefabName);
            _gameObject = GameObject.Instantiate(go,trans);
            _gameObject.SetActive(false);
            _identity = _gameObject.AddComponent<Identity>();
            var actorMono=_gameObject.AddComponent<ActorMono>();
            actorMono.SetActor(this);
            _gameObject.SetActive(true);
        }


        public Identity GetIdentity() { return _identity; }
        
        
        public GameObject GetGameObject() { return _gameObject; }

        public T AddComponent<T>() where T: Component { return _gameObject.AddComponent<T>(); }
        
        
        public int GetIndex() { return Index; }
        
        public void SetIndex(int index) { Index = index; }
        
        public virtual void Awake() {}

        public virtual void Start() {}

        public virtual void OnEnable() {}

        public virtual void OnDisable() {}

        public virtual void Update() {}

        public virtual void FixedUpdate() {}

        public virtual void LateUpdate() {}

        public virtual void OnDestroy() {}

        protected void Registered(int eventType,int id,Action<object[]> evt)
        {
            EventManager.AddListener(eventType,id,evt);
        }
        
        protected void Registered(Enum eventType,Enum id,Action<object[]> evt)
        {
            EventManager.AddListener(eventType,id,evt);
        }
        
        protected void Unbinding(int eventType,int id,Action<object[]> evt)
        {
            EventManager.RemoveListener(eventType,id,evt);
        }
        
        protected void Unbinding(Enum eventType,Enum id,Action<object[]> evt)
        {
            EventManager.RemoveListener(eventType,id,evt);
        }

    }
}