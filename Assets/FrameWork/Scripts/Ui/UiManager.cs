using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsClass<UiManager>
    {
        
        // private Transform CanvasTransform = null;
        //
        // private Transform BackgroundTransform = null;
        //
        // private Transform NormalTransform = null;
        //
        // private Transform PopupTransform = null;
        //
        // private Transform ControlTransform = null;

        private int _index;
        
        
        private Stack<int> _uiStack;
        private Dictionary<Type, UiActor> _actors;
        private List<int> _uiList;
        private Actor _uiRoot;
        public void Init()
        {
            _index = 0;
            //_uiStack.Clear();
            ClearAllPanel();
        }

        public UiManager()
        {
            _uiList = new List<int>();
            _actors = new Dictionary<Type, UiActor>();
            _uiStack = new Stack<int>();
            //var prefab = AssetBundlesLoad.LoadAsset<GameObject>("Ui", "UiRoot");
            //CanvasTransform= GameObject.Instantiate(prefab)?.transform;
            // var type = DllLoad.GetHoyUpdateDllType("FrameWork.UiRoot");
            _uiRoot = new UiRoot();
            // if (_uiRoot!=null)
            // {
            //     CanvasTransform = _uiRoot.GetGameObject().transform;
            //     if (CanvasTransform!=null)
            //     {
            //         BackgroundTransform =CanvasTransform.Find("Background");
            //         NormalTransform =CanvasTransform.Find("Normal");
            //         PopupTransform =CanvasTransform.Find("Popup");
            //         ControlTransform =CanvasTransform.Find("Control");
            //     }
            // }
        }


        public T GetUi<T>() where T: UiActor
        {
            if (_actors.ContainsKey(typeof(T)))
            {
                return (T)_actors[typeof(T)];
            }

            return null;
        }

        public void ShowUi(int index)
        {
            //EventManager.DispatchEvent(MessageType.UiMessage,UiMessageType.Show,new object[]{index});
            var msg=EventManager.GetEventMsg();
            msg.Add(index);
            Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Show,msg);
            if (!_uiStack.Contains(index))
            {
                _uiStack.Push(index);
                _uiList.Add(index);
            }
            
            //ShowUiAction?.Invoke(index);
        }
        
        public UiActor ShowUi(string type,object[] objects=null)
        {
            var t = Assembly.GetExecutingAssembly().GetType(type);
            return ShowUi(t,objects);
        }


        
        
        public UiActor ShowUi(Type type,object[] objects=null)
        {
            if (_uiRoot.GetGameObject()==null)
            {
                MyLog.LogError("场景中没有Canvas组件,无法显示Ui物体");
                return null;
            }
            
            Type t = type;

            if (_actors.TryGetValue(type,out UiActor actor))
            {
                ShowUi(actor.GetIndex());
                actor.Open(objects);
                return actor;
            }
            
            
            
            string fullName = t.Name;
            var uiMode=t.GetCustomAttribute<UiModeAttribute>();

            if (uiMode==null)
            {
                MyLog.LogError("类不具备UiModeAttribute");
                return null;
            }
           
            Transform tran=GetTransform(uiMode);

            var param = new object[] { tran };
            UiActor obj;
            try
            {
                obj= (UiActor)Activator.CreateInstance(t, param);
            }
            catch (Exception e)
            {
                
                MyLog.LogError(e.Message);
                return null;
            }
            
            //var obj =(T)Assembly.GetExecutingAssembly().CreateInstance(t.Namespace+"."+fullName)
            if (obj==null)
            {
                MyLog.LogError("生成ui失败");
                return null;
            }
            
            obj.SetIndex(_index);
            _index += 1;
            _actors.Add(type,obj);
            _uiStack.Push(obj.GetIndex());
            _uiList.Add(obj.GetIndex());
            obj.Open(objects);
            var msg=EventManager.GetEventMsg();
            msg.Add(obj.GetIndex());
            EventManager.DispatchEvent((int)MessageType.UiMessage,(int)UiMessageType.Show,msg);
            return obj;
        }
        
        

        public T ShowUi<T>(object[] objects=null) where T: UiActor
        {
            return (T)ShowUi(typeof(T),objects);
        }
        public void HideUi(int index)
        {
            //EventManager.DispatchEvent(MessageType.UiMessage,UiMessageType.Hide,new object[]{index});
            var msg=EventManager.GetEventMsg();
            msg.Add(index);
            Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Hide,msg);
            _uiList.Remove(index);
            //HideUiAction?.Invoke(index);
        }

        
        public void HideUi<T>()
        {
            if (_actors.ContainsKey(typeof(T)))
            {
                
                HideUi(_actors[typeof(T)].GetIndex());
                //Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Hide,new object[]{_actors[typeof(T)].GetIndex()});
            }
            //EventManager.DispatchEvent(MessageType.UiMessage,UiMessageType.Hide,new object[]{index});
            //HideUiAction?.Invoke(index);
        }
        
        public void RemoveUi(int index)
        {
            //EventManager.DispatchEvent(MessageType.UiMessage,UiMessageType.Remove,new object[]{index});
            var msg=EventManager.GetEventMsg();
            msg.Add(index);
            Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Remove,msg);
            _uiList.Remove(index);
            //RemoveUiAction?.Invoke(index);
        }

        public void RemoveUi<T>()
        {
            if (_actors.ContainsKey(typeof(T)))
            {
                RemoveUi(_actors[typeof(T)].GetIndex());
                _actors.Remove(typeof(T));
            }
        }

        public bool UiIsOpen<T>()
        {
            if (_actors.ContainsKey(typeof(T)))
            {
                return _actors[typeof(T)].GetGameObject().activeSelf;
            }

            return false;
        }
        public int Back()
        {
            if (_uiStack.Count>0)
            {
                var index=_uiStack.Pop();

                if (!_uiList.Contains(index))
                {
                    return Back();
                }

                _uiList.Remove(index);
                HideUi(index);
                return index;
            }
            return -1;
        }
        
        public void ClearAllPanel()
        {
            RemoveUi(-1);
            //RemoveUiAction?.Invoke(-1);
            _uiStack.Clear();
            _uiList.Clear();
        }
        
        public void HideAllPanel()
        {
            HideUi(-1);
            //RemoveUiAction?.Invoke(-1);
            _uiStack.Clear();
            _uiList.Clear();
        }


        public Transform GetTransform(UiModeAttribute uiModeAttribute)
        {
            Transform tran = _uiRoot.GetGameObject().transform.Find(uiModeAttribute.Mode.ToString());
            

            return tran;
        }
        
        
        protected void Registered(int eventType,int id,Action<List<object>> evt)
        {
            EventManager.AddListener(eventType,id,evt);
        }
        
        // protected void Registered(Enum eventType,Enum id,Action<object[]> evt)
        // {
        //     EventManager.AddListener((int)eventType,(int)id,evt);
        // }
        
        protected void Unbinding(int eventType,int id,Action<List<object>> evt)
        {
            EventManager.RemoveListener(eventType,id,evt);
        }
        
        // protected void Unbinding(Enum eventType,Enum id,Action<object[]> evt)
        // {
        //     EventManager.RemoveListener(eventType,id,evt);
        // }

        
        protected void Dispatch(int evtType, int evt, List<object> data = null)
        {
            EventManager.DispatchEvent((int)evtType,(int)evt,data);
        }
    }
}