using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsClass<UiManager>
    {
        
        private Transform CanvasTransform = null;

        private Transform BackgroundTransform = null;
        
        private Transform NormalTransform = null;
        
        private Transform PopupTransform = null;
        
        private Transform ControlTransform = null;

        private int _index;


        public Action<int> ShowUiAction;
        public Action<int> HideUiAction;
        public Action<int> RemoveUiAction;
        
        private Stack<Actor> _uiStack;

        private UiRoot _uiRoot;
        public void Init()
        {
            _index = 0;
            //_uiStack.Clear();
            ClearAllPanel();
        }

        public UiManager()
        {
            _uiStack = new Stack<Actor>();
            //var prefab = AssetBundlesLoad.LoadAsset<GameObject>("Ui", "UiRoot");
            //CanvasTransform= GameObject.Instantiate(prefab)?.transform;

            _uiRoot = new UiRoot();
            CanvasTransform = _uiRoot.GetGameObject().transform;
            if (CanvasTransform!=null)
            {
                BackgroundTransform =CanvasTransform.Find("Background");
                NormalTransform =CanvasTransform.Find("Normal");
                PopupTransform =CanvasTransform.Find("Popup");
                ControlTransform =CanvasTransform.Find("Control");
            }
        }


        public void ShowUi(int index)
        {
            ShowUiAction?.Invoke(index);
        }


        public void ShowUi<T>() where T: Actor
        {

            if (CanvasTransform==null)
            {
                Debug.LogError("场景中没有Canvas组件,无法显示Ui物体");
                return;
            }
            
            Type t = typeof(T);
            string fullName = t.Name;
            var uiMode=t.GetCustomAttribute<UiModeAttribute>();

            if (uiMode==null)
            {
                Debug.LogError("类不具备UiModeAttribute");
                return;
            }
            
            var obj =(T)Assembly.GetExecutingAssembly().CreateInstance(typeof(T).Name);
            //GameObject prefab = AssetBundlesLoad.LoadAsset<GameObject>(obj.GetPack(), obj.GetPrefabName());
            if (obj==null)
            {
                Debug.LogError("生成ui失败");
                return ;
            }
            
            Transform tran=null;
            switch (uiMode.Mode)
            {
                case Mode.Background:
                    tran = BackgroundTransform;
                    break;
                case Mode.Normal:
                    tran = NormalTransform;
                    break;
                case Mode.Popup:
                    tran = PopupTransform;
                    break;
                case Mode.Control:
                    tran = ControlTransform;
                    break;
            }
            obj.GetGameObject().transform.SetParent(tran);
            obj.SetIndex(_index);
            _index += 1;
            _uiStack.Push(obj);
        }
        public void HideUi(int index)
        {
            HideUiAction?.Invoke(index);
        }

        
        public void RemoveUi(int index)
        {
            RemoveUiAction?.Invoke(index);
        }


        public void Back()
        {
            if (_uiStack.Count>0)
            {
                var actor=_uiStack.Pop();
                if (actor!=null)
                {
                    RemoveUiAction?.Invoke(actor.GetIndex());
                }
                else
                {
                    Back();
                }
            }
        }
        
        public void ClearAllPanel()
        {
            RemoveUiAction?.Invoke(-1);
            _uiStack.Clear();
        }
        
        

    }
}