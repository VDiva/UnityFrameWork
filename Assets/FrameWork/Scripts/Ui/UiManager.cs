using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsMono<UiManager>
    {

        private int _index;
        private Stack<int> _uiStack;
        private ConcurrentDictionary<Type, UiActor> _actors;
        private Actor _uiRoot;
        public void Init()
        {
            _index = 0;
            ClearAllPanel();
        }

        public UiManager()
        {
            _actors = new ConcurrentDictionary<Type, UiActor>();
            _uiStack = new Stack<int>();
            _uiRoot = (Actor)GetType().Assembly.CreateInstance("FrameWork.UiRoot");
                
        }

        private void Awake()
        {
            DontDestroyOnLoad(this);
            DontDestroyOnLoad(_uiRoot.GetGameObject());
        }
        public void ShowUi(int index)
        {
            var data = EventManager.GetEventMsg();
            data.Add(index);
            Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Show,data);
            if (!_uiStack.Contains(index))
            {
                _uiStack.Push(index);
            }

        }
        
        public UiActor ShowUi(string type)
        {
            var t = Assembly.GetExecutingAssembly().GetType(type);
            return ShowUi(t);
        }
        
        
        public UiActor ShowUi(Type type)
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
            _actors.TryAdd(type,obj);
            _uiStack.Push(obj.GetIndex());
            return obj;
        }
        

        public T ShowUi<T>() where T: UiActor
        {
            return (T)ShowUi(typeof(T));
        }
        public void HideUi(int index)
        {
            //EventManager.DispatchEvent(MessageType.UiMessage,UiMessageType.Hide,new object[]{index});
            var data = EventManager.GetEventMsg();
            data.Add(index);
            Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Hide,data);
            //HideUiAction?.Invoke(index);
        }

        
        public void RemoveUi(int index)
        {
            //EventManager.DispatchEvent(MessageType.UiMessage,UiMessageType.Remove,new object[]{index});
            var data = EventManager.GetEventMsg();
            data.Add(index);
            Dispatch((int)MessageType.UiMessage,(int)UiMessageType.Remove,data);
            //RemoveUiAction?.Invoke(index);
        }
        
        public int Back()
        {
            if (_uiStack.Count>0)
            {
                var index=_uiStack.Pop();
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
        }
        
        public void HideAllPanel()
        {
            HideUi(-1);
            //RemoveUiAction?.Invoke(-1);
            _uiStack.Clear();
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