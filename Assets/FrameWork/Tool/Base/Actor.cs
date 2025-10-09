using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class Actor
    {
        //public string ActorName;
        private int Index=-999999;
        private GameObject _gameObject;

        //private Identity _identity;
        
        public Transform transform => _gameObject.transform;
        
        public Actor()
        {
            var type=GetType();
            var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            if (infoAttribute==null||infoAttribute.PackName==""|| infoAttribute.PrefabName=="")return;
            GameObject go=null;
            go=ABMrg.Load<GameObject>(infoAttribute.PrefabName);
            _gameObject = GameObject.Instantiate(go);
            _gameObject.SetActive(false);
            //_identity = _gameObject.AddComponent<Identity>();
            var actorMono=_gameObject.AddComponent<ActorMono>();
            actorMono.SetActor(this);
            _gameObject.SetActive(true);
        }
       
        public Actor(Transform trans)
        {
            var type=GetType();
            var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            if (infoAttribute==null||infoAttribute.PackName==""|| infoAttribute.PrefabName=="")return;
            GameObject go=null;
            go=ABMrg.Load<GameObject>(infoAttribute.PrefabName);
            _gameObject = GameObject.Instantiate(go,trans);
            _gameObject.SetActive(false);
            //_identity = _gameObject.AddComponent<Identity>();
            var actorMono=_gameObject.AddComponent<ActorMono>();
            actorMono.SetActor(this);
            _gameObject.SetActive(true);
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
        
        protected void AddListener(int eventType,int id,Action<List<object>> evt)
        {
            EventManager.AddListener(eventType,id,evt);
        }
        
        protected void RemoveListener(int eventType,int id,Action<List<object>> evt)
        {
            EventManager.RemoveListener(eventType,id,evt);
        }
        
        protected void DispatchEvent(int evtType, int evt, List<object> data = null)
        {
            EventManager.DispatchEvent((int)evtType,(int)evt,data);
        }
        
        
        
        protected void AddListener(Enum eventType,Enum id,Action<List<object>> evt)
        {
            EventManager.AddListener((int)(object)eventType,(int)(object)id,evt);
        }
        
        protected void RemoveListener(Enum eventType,Enum id,Action<List<object>> evt)
        {
            EventManager.RemoveListener((int)(object)eventType,(int)(object)id,evt);
        }
        
        protected void DispatchEvent(Enum evtType, Enum evt, List<object> data = null)
        {
            EventManager.DispatchEvent((int)(object)evtType,(int)(object)evt,data);
        }

    }
}