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
        
        public Actor()
        {
            // var type=GetType();
            // var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            // if (infoAttribute==null||infoAttribute.PrefabName=="")return;
            // GameObject go=null;
            // go=ABMrg.Load<GameObject>(infoAttribute.PrefabName);
            // _gameObject = GameObject.Instantiate(go);
            // _gameObject.SetActive(false);
            // //_identity = _gameObject.AddComponent<Identity>();
            // var actorMono=_gameObject.AddComponent<ActorMono>();
            // actorMono.SetActor(this);
            // _gameObject.SetActive(true);
            _=SpawnObject(null);
        }
       
        public Actor(Transform trans)
        {
            // var type=GetType();
            // var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            // if (infoAttribute==null||infoAttribute.PrefabName=="")return;
            // GameObject go=null;
            // go=ABMrg.Load<GameObject>(infoAttribute.PrefabName);
            // _gameObject = GameObject.Instantiate(go,trans);
            // _gameObject.SetActive(false);
            // //_identity = _gameObject.AddComponent<Identity>();
            // var actorMono=_gameObject.AddComponent<ActorMono>();
            // actorMono.SetActor(this);
            // _gameObject.SetActive(true);
            _=SpawnObject(trans);
        }


        public async UniTask<Actor> SpawnObject(Transform trans)
        {
            var type=GetType();
            var infoAttribute=type.GetCustomAttribute<ActorInfoAttribute>();
            if (infoAttribute==null||infoAttribute.PrefabName=="")return null;
            //GameObject go=null;
            _gameObject=await ABMrg.InstantiateAsync(infoAttribute.PrefabName);
            //_gameObject = GameObject.Instantiate(go,trans);
            _gameObject.SetActive(false);
            _gameObject.transform.SetParent(trans);
            _gameObject.transform.localPosition = Vector3.zero;
            _gameObject.transform.localRotation = Quaternion.identity;
            _gameObject.transform.localScale = Vector3.one;
            //_identity = _gameObject.AddComponent<Identity>();
            var actorMono=_gameObject.AddComponent<ActorMono>();
            actorMono.SetActor(this);
            Awake();
            _gameObject.SetActive(true);
            Start();

            if (trans)
            {
                try
                {
                    var uiAtor = this as UiActor;
                    uiAtor?.ResetRect();
                    uiAtor?.OpenAnim();
                }
                catch (Exception e)
                {
                    Debug.LogError("转换失败:"+e.Message);
                }
                
            }
            
            return this;
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