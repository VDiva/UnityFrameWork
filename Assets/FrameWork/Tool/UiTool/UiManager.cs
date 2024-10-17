using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public static class UiManager 
    {
        
        private static int _index;
        private static Dictionary<Type, UiActor> _uiDic;
        private static Dictionary<int, Type> _typesDic;
        private static List<int> _uiList;
        private static Actor _uiRoot;
        public static void Init()
        {
            _index = 0;
        }

        static UiManager()
        {
            _uiDic = new Dictionary<Type, UiActor>();
            _typesDic = new Dictionary<int, Type>();
            _uiList = new List<int>();
            _uiRoot = new UiRoot();
            GameObject.DontDestroyOnLoad(_uiRoot.GetGameObject());
        }


        
        
        public static T GetUi<T>() where T: UiActor
        {
            if (_uiDic.ContainsKey(typeof(T)))
            {
                return (T)_uiDic[typeof(T)];
            }
            return null;
        }


        public static void OpenUi(int index,object[] objs=null)
        {
            if (_typesDic.ContainsKey(index))
            {
                OpenUi(_typesDic[index]);
            }
        }
        
        public static T OpenUi<T>(object[] objs=null) where T: UiActor
        {
            return (T)OpenUi(typeof(T), objs);
        }

        
        
        
        public static UiActor OpenUi(Type type,object[] objects=null)
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
                _uiDic[type].transform.SetAsLastSibling();
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
                var ui = Activator.CreateInstance(type, param);
                obj= (UiActor)ui;
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
            obj.Open(objects);
            obj.SetIndex(_index);
            _uiDic.Add(type,obj);
            _uiList.Add(obj.GetIndex());
            _typesDic.Add(obj.GetIndex(),type);
            _index += 1;
            return obj;
        }

        
        public static bool IsOpenUi<T>()
        {
            if (_uiDic.ContainsKey(typeof(T)))
            {
                return true;
            }

            return false;
        }

        public static T HideUi<T>() where T : UiActor
        {
            return (T)HideUi(typeof(T));
        }
        
        
        public static UiActor HideUi(Type type) 
        {
            if (_uiDic.ContainsKey(type))
            {
                var uiActor=_uiDic[type];
                uiActor.OnClose();
                uiActor.SetActive(false);
                _uiDic[type].SetActive(false);
                _uiList.Remove(uiActor.GetIndex());
                return uiActor;
            }

            return null;
        }
        
        
        public static void HideUi(int index)
        {
            if (_typesDic.ContainsKey(index))
            {
                HideUi(_typesDic[index]);
            }
        }


        public static void RemoveUi<T>() where T: UiActor
        {
            RemoveUi(typeof(T));
        }
        
        public static void RemoveUi(Type type)
        {
            if (_uiDic.ContainsKey(type))
            {
                var uiActor=_uiDic[type];
                uiActor.OnClose();
                _typesDic.Remove(uiActor.GetIndex());
                _uiList.Remove(uiActor.GetIndex());
                _uiDic.Remove(type);
                GameObject.Destroy(uiActor.GetGameObject());
            }
            
        }
        
        public static void RemoveUi(int index)
        {
            if (_typesDic.ContainsKey(index))
            {
                RemoveUi(_typesDic[index]);
            }
        }



        public static void Back()
        {
            if (_uiList.Count>0)
            {
                var index = _uiList.Last();
                HideUi(index);
            }
        }

        private static Dictionary<string, Transform> _layerDic = new Dictionary<string, Transform>();
        public static Transform GetTransform(UiModeAttribute uiModeAttribute)
        {

            if (_layerDic.ContainsKey(uiModeAttribute.Mode.ToString()))
            {
                return _layerDic[uiModeAttribute.Mode.ToString()];
            }
            
            Transform tran = _uiRoot.GetGameObject().transform.Find(uiModeAttribute.Mode.ToString());
            if (tran==null)
            {
                var layer=GameObject.Instantiate(_uiRoot.GetGameObject().transform.GetChild(0), _uiRoot.GetGameObject().transform);
                layer.name = uiModeAttribute.Mode.ToString();
                tran = layer;
                _layerDic.Add(uiModeAttribute.Mode.ToString(),tran);
            }
            else
            {
                if (!_layerDic.ContainsKey(uiModeAttribute.Mode.ToString()))
                {
                    _layerDic.Add(uiModeAttribute.Mode.ToString(),tran);
                }
            }
            return tran;
        }
        
    }
}