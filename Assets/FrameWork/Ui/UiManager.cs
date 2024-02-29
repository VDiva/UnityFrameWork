using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsClass<UiManager>
    {
        private Dictionary<int, Actor> UiDic;

        private Dictionary<string, Actor> SingleUiDic;

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
        public int UiCount
        {
            get { return UiDic.Count; }
        }


        public void Init()
        {
            _index = 0;
            //_uiStack.Clear();
            ClearAllPanel();
        }

        public UiManager()
        {
            UiDic = new Dictionary<int, Actor>();
            SingleUiDic = new Dictionary<string, Actor>();
            _uiStack = new Stack<Actor>();
            var prefab = AssetBundlesLoad.LoadAsset<GameObject>(AssetType.Ui.ToString(), "UiRoot");
            CanvasTransform= GameObject.Instantiate(prefab)?.transform;
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

            if (uiMode.UiType.Equals(UiType.Single))
            {
                var ui =GameObject.FindObjectOfType<T>();
                if (ui!=null)
                {
                    Debug.LogWarning("尝试显示多个单一ui 但不会起到效果");
                    return;
                }
                else
                {
                    if (SingleUiDic.TryGetValue(fullName,out Actor showUi))
                    {
                        showUi.SetActive(true);
                        return;
                    }
                }
            }


            GameObject prefab = AssetBundlesLoad.LoadAsset<GameObject>(AssetType.Ui, fullName);
            if (prefab==null)
            {
                Debug.LogError("找不到需要生成的预制体");
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
            
            GameObject go =GameObject.Instantiate(prefab,tran==null? CanvasTransform: tran);
            var actor=go.AddComponent<T>();
            actor.SetIndex(_index);
            actor.SetActorName(fullName);
            _index += 1;
            
            _uiStack.Push(actor);
            
            //UiDic.Add(actor.GetIndex(),actor);    
            
            if (uiMode.UiType.Equals(UiType.Single))SingleUiDic.Add(fullName,actor);
            return;
        }

        
        public void HideUi(int index)
        {
            //string fullName = typeof(T).Name;
            // if (UiDic.TryGetValue(index,out Actor actor))
            // {
            //     //UiDic.Remove(uiBase.Name);
            //     //GameObject.Destroy(uiBase.UiGameObject);
            //     actor.SetActive(false);
            // }
            
            HideUiAction?.Invoke(index);
        }

        public void HideUi<T>() where T : UiActor
        {
            Type t = typeof(T);
            string fullName = t.Name;
            var uiMode=t.GetCustomAttribute<UiModeAttribute>();
            if (uiMode!=null)
            {
                if (uiMode.UiType.Equals(UiType.Single))
                {
                    var ui=GameObject.FindObjectOfType<T>();
                    if (ui!=null)
                    {
                        ui.SetActive(false);
                    }
                    else
                    {
                        Debug.Log("场景中没有此ui");
                    }
                }
                else
                {
                    Debug.Log("不是单一得ui不能使用此方法隐藏");
                }
            }
            else
            {
                Debug.Log("类不具备UiModeAttribute");
            }
        }
        
        
        
        
        public void RemoveUi<T>() where T : UiActor
        {
            Type t = typeof(T);
            string fullName = t.Name;
            var uiMode=t.GetCustomAttribute<UiModeAttribute>();
            if (uiMode!=null)
            {
                if (uiMode.UiType.Equals(UiType.Single))
                {
                    var ui=GameObject.FindObjectOfType<T>();
                    if (ui!=null)
                    {
                        RemoveUi(ui.GetIndex());
                    }
                    else
                    {
                        if (SingleUiDic.TryGetValue(fullName,out Actor actor))
                        {
                            RemoveUi(actor.GetIndex());
                            //SingleUiDic.Remove(fullName);
                        }
                        else
                        {
                            Debug.Log("场景中没有此ui"); 
                        }
                    }
                }
                else
                {
                    Debug.Log("不是单一得ui不能使用此方法删除");
                }
            }
            else
            {
                Debug.Log("类不具备UiModeAttribute");
            }
        }
        
        
        public void RemoveUi(int index)
        {
            RemoveUiAction?.Invoke(index);
        }

        public void RemoveSu(string name)
        {
            SingleUiDic.Remove(name);

        }
        
        // public void RemoveUi(GameObject go)
        // {
        //     var actor = go.GetComponent<Actor>();
        //     if (actor!=null)
        //     {
        //         UiDic.Remove(actor.GetIndex());
        //         if (SingleUiDic.ContainsKey(actor.GetActorName())) SingleUiDic.Remove(actor.GetActorName());
        //         GameObject.Destroy(actor.gameObject);
        //     }
        // }

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
                Actor uiActor = UiDic[key];
                if (uiActor != null)
                {
                    GameObject.Destroy(uiActor.gameObject);
                }
            }
            UiDic.Clear();
            _uiStack.Clear();
        }
        
        

    }
}