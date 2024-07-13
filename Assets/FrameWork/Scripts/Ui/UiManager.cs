using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsClass<UiManager>
    {
        
        private int _index;
        private Dictionary<Type, UiActor> _uiDic;
        private Dictionary<int, Type> _typesDic;
        private List<int> _uiList;
        private Actor _uiRoot;
        public void Init()
        {
            _index = 0;
        }

        public UiManager()
        {
            _uiDic = new Dictionary<Type, UiActor>();
            _typesDic = new Dictionary<int, Type>();
            _uiList = new List<int>();
            _uiRoot = new UiRoot();
            GameObject.DontDestroyOnLoad(_uiRoot.GetGameObject());
        }


        public T GetUi<T>() where T: UiActor
        {
            if (_uiDic.ContainsKey(typeof(T)))
            {
                return (T)_uiDic[typeof(T)];
            }
            return null;
        }


        public void OpenUi(int index,object[] objs=null)
        {
            if (_typesDic.ContainsKey(index))
            {
                OpenUi(_typesDic[index]);
            }
        }
        
        public T OpenUi<T>(object[] objs=null) where T: UiActor
        {
            return (T)OpenUi(typeof(T), objs);
        }

        
        
        public UiActor OpenUi(Type type,object[] objects=null)
        {
            if (_uiRoot.GetGameObject()==null)
            {
                MyLog.LogError("场景中没有Canvas组件,无法显示Ui物体");
                return null;
            }

            if (_uiDic.ContainsKey(type))
            {
                _uiDic[type].SetActive(true);
                _uiDic[type].Open(objects);
                if (!_uiList.Contains(_uiDic[type].GetIndex()))
                {
                    
                    _uiList.Add(_uiDic[type].GetIndex());
                }
                return _uiDic[type];
            }
            
            var uiMode=type.GetCustomAttribute<UiModeAttribute>();
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
                obj= (UiActor)Activator.CreateInstance(type, param);
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
            _uiDic.Add(type,obj);
            _uiList.Add(obj.GetIndex());
            _typesDic.Add(obj.GetIndex(),type);
            _index += 1;
            return obj;
        }


        public T HideUi<T>() where T : UiActor
        {
            return (T)HideUi(typeof(T));
        }
        
        
        public UiActor HideUi(Type type) 
        {
            if (_uiDic.ContainsKey(type))
            {
                var uiActor=_uiDic[type];
                uiActor.SetActive(false);
                _uiDic[type].SetActive(false);
                _uiList.Remove(uiActor.GetIndex());
                return uiActor;
            }

            return null;
        }
        
        
        public void HideUi(int index)
        {
            if (_typesDic.ContainsKey(index))
            {
                HideUi(_typesDic[index]);
            }
        }


        public void RemoveUi<T>() where T: UiActor
        {
            RemoveUi(typeof(T));
        }
        
        public void RemoveUi(Type type)
        {
            if (_uiDic.ContainsKey(type))
            {
                var uiActor=_uiDic[type];
                _typesDic.Remove(uiActor.GetIndex());
                _uiList.Remove(uiActor.GetIndex());
                _uiDic.Remove(type);
                GameObject.Destroy(uiActor.GetGameObject());
            }
            
        }
        
        public void RemoveUi(int index)
        {
            if (_typesDic.ContainsKey(index))
            {
                RemoveUi(_typesDic[index]);
            }
        }



        public void Back()
        {
            if (_uiList.Count>0)
            {
                var index = _uiList.Last();
                HideUi(index);
            }
        }
        
        public Transform GetTransform(UiModeAttribute uiModeAttribute)
        {
            Transform tran = _uiRoot.GetGameObject().transform.Find(uiModeAttribute.Mode.ToString());
            if (tran==null)
            {
                var layer=GameObject.Instantiate(_uiRoot.GetGameObject().transform.GetChild(0), _uiRoot.GetGameObject().transform);
                layer.name = uiModeAttribute.Mode.ToString();
                tran = layer;
            }
            return tran;
        }
        
    }
}