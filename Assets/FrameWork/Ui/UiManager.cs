using System;
using System.Collections.Generic;
using System.Reflection;
using FrameWork.Attribute;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsMono<UiManager>
    {
        private Dictionary<int, Actor> UiDic;

        private Transform CanvasTransform = null;

        private Transform BackgroundTransform = null;
        
        private Transform NormalTransform = null;
        
        private Transform PopupTransform = null;
        
        private Transform ControlTransform = null;

        private int _index;
        
        private Stack<Actor> _uiStack;
        public int UiCount
        {
            get { return UiDic.Count; }
        }


        public void Init()
        {
            _index = 0;
            _uiStack.Clear();
        }
        
        private void Awake()
        {
            UiDic = new Dictionary<int, Actor>();
            _uiStack = new Stack<Actor>();
            var prefab = AssetBundlesLoad.LoadAsset<GameObject>(GlobalVariables.Configure.AbModePrefabName, "UiRoot");
            
            CanvasTransform= GameObject.Instantiate(prefab)?.transform;
            if (CanvasTransform!=null)
            {
                BackgroundTransform =CanvasTransform.Find("Background");
                NormalTransform =CanvasTransform.Find("Normal");
                PopupTransform =CanvasTransform.Find("Popup");
                ControlTransform =CanvasTransform.Find("Control");
            }
        }

        public Actor ShowUi<T>(int index=-1) where T: Actor
        {
            Type t = typeof(T);
            string fullName = t.Name;
            
            if (UiDic.ContainsKey(index))
            {
                UiDic[index].SetActive(true);
                //Debug.Log("当前界面已经显示了,名字:"+fullName);
                return UiDic[index];
            }

            if (CanvasTransform==null)
            {
                Debug.LogError("场景中没有Canvas组件,无法显示Ui物体");
                return null;
            }
            
            //UiBase uiBase = Activator.CreateInstance(t);
            
            GameObject prefab = AssetBundlesLoad.LoadAsset<GameObject>(GlobalVariables.Configure.AbModePrefabName, fullName);
            if (prefab==null)
            {
                Debug.LogError("找不到需要生成的预制体");
                return null;
            }

            var uiMode=t.GetCustomAttribute<UiModeAttribute>();
            
            Transform tran=null;
            switch (uiMode.UiType)
            {
                case UiType.Background:
                    tran = BackgroundTransform;
                    break;
                case UiType.Normal:
                    tran = NormalTransform;
                    break;
                case UiType.Popup:
                    tran = PopupTransform;
                    break;
                case UiType.Control:
                    tran = ControlTransform;
                    break;
            }
            
            GameObject go = Instantiate(prefab,tran==null? CanvasTransform: tran);
            var actor=go.AddComponent<T>();
            actor.SetIndex(_index);
            _index += 1;
            
            _uiStack.Push(actor);
            
            UiDic.Add(actor.GetIndex(),actor);
            return actor;
        }

        
        public void HideUi(int index)
        {
            //string fullName = typeof(T).Name;
            if (UiDic.TryGetValue(index,out Actor actor))
            {
                //UiDic.Remove(uiBase.Name);
                //GameObject.Destroy(uiBase.UiGameObject);
                actor.SetActive(false);
            }
        }
        
        public void RemoveUi(int index)
        {
            //string fullName = typeof(T).Name;
            if (UiDic.TryGetValue(index,out Actor actor))
            {
                UiDic.Remove(actor.GetIndex());
                GameObject.Destroy(actor.gameObject);
            }
        }

        
        public void RemoveUi(GameObject go)
        {
            var actor = go.GetComponent<Actor>();
            if (actor!=null)
            {
                UiDic.Remove(actor.GetIndex());
                GameObject.Destroy(actor.gameObject);
            }
        }

        public void Back()
        {
            if (_uiStack.Count>0)
            {
                var actor=_uiStack.Pop();
                if (actor!=null)
                {
                    UiDic.Remove(actor.GetIndex());
                    GameObject.Destroy(actor.gameObject);
                }
                else
                {
                    Back();
                }
            }
        }
        
        public void ClearAllPanel()
        {
            foreach (var key in UiDic.Keys)
            {
                Actor uiBase = UiDic[key];
                if (uiBase != null)
                {
                    GameObject.Destroy(uiBase.gameObject);
                }
            }
            UiDic.Clear();
            _uiStack.Clear();
        }
        
        

    }
}