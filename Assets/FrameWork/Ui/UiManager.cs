using System;
using System.Collections.Generic;
using System.Reflection;
using FrameWork.Attribute;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsMono<UiManager>
    {
        private Dictionary<string, UiBase> UiDic;

        private Transform CanvasTransform = null;

        private Transform BackgroundTransform = null;
        
        private Transform NormalTransform = null;
        
        private Transform PopupTransform = null;
        
        
        
        private Stack<UiBase> _uiStack;
        public int UiCount
        {
            get { return UiDic.Count; }
        }
        
        private void Awake()
        {
            UiDic = new Dictionary<string, UiBase>();
            _uiStack = new Stack<UiBase>();
            var prefab = AssetBundlesLoad.LoadAsset<GameObject>(GlobalVariables.Configure.AbModePrefabName, "UiRoot");
            
            CanvasTransform= GameObject.Instantiate(prefab)?.transform;
            if (CanvasTransform!=null)
            {
                BackgroundTransform =CanvasTransform.Find("Background");
                NormalTransform =CanvasTransform.Find("Normal");
                PopupTransform =CanvasTransform.Find("Popup");
            }
        }

        public UiBase ShowUi<T>()
        {
            Type t = typeof(T);
            string fullName = t.Name;
        
            if (UiDic.ContainsKey(fullName))
            {
                Debug.Log("当前界面已经显示了,名字:"+fullName);
                return UiDic[fullName];
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
            
            UiBase uiBase=Activator.CreateInstance<UiBase>();

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
            }
            
            GameObject go = Instantiate(prefab,tran==null? CanvasTransform: tran);
            uiBase.UiGameObject = go;
            uiBase.Name = fullName;
            _uiStack.Push(uiBase);
            UiDic.Add(fullName,uiBase);
            return uiBase;
        }

        public void RemoveUi<T>()
        {
            string fullName = typeof(T).Name;
            if (UiDic.TryGetValue(fullName,out UiBase uiBase))
            {
                UiDic.Remove(uiBase.Name);
                GameObject.Destroy(uiBase.UiGameObject);
            }
        }


        public void Back()
        {
            if (_uiStack.Count>0)
            {
                var uiBase=_uiStack.Pop();
                if (uiBase!=null)
                {
                    UiDic.Remove(uiBase.Name);
                    GameObject.Destroy(uiBase.UiGameObject);
                }
            }
        }
        
        public void ClearAllPanel()
        {
            foreach (var key in UiDic.Keys)
            {
                UiBase uiBase = UiDic[key];
                if (uiBase != null)
                {
                    GameObject.Destroy(uiBase.UiGameObject);
                }
            }
            UiDic.Clear();
        }
        
        
        void SetRectTransAsFullScreen(RectTransform rectTrans)
        {
            rectTrans.localPosition = Vector3.zero;
            rectTrans.sizeDelta = Vector2.zero;
            rectTrans.localScale = Vector3.one;
            rectTrans.anchorMin = Vector2.zero;
            rectTrans.anchorMax = Vector2.one;
        }

    }
}