using UnityEngine;

namespace FrameWork
{
    public class UiBase
    {
        
        public GameObject UiGameObject;
        
        
        public virtual void Start(){}
        
        public virtual void Update(){}
        
        public virtual void Destroy(){}

        public bool GetChildrenObject(string name,out GameObject go)
        {
            Transform[] trans = UiGameObject.GetComponentsInChildren<Transform>();
            for (int i = 0; i < trans.Length; i++)
            {
                if (trans[i].name==name)
                {
                    go= trans[i].gameObject;
                    return true;
                }
            }
            
            Debug.LogError($"找不到名为 {name} 的对象");
            go = null;
            return false;
        }
        
        
        public bool GetChildrenComponent<T>(string name,out T component) where T: Component
        {
            if (GetChildrenObject(name,out GameObject go))
            {
                component = go.GetComponent<T>();
                return true;
            }

            component = null;
            return false;
        }

        protected UiBase(){}
    }
}