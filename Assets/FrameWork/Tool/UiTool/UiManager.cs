using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

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
            _=CheckUiRoot();
            
        }

        private static async UniTask CheckUiRoot()
        {
            await UniTask.WaitUntil(() => _uiRoot.GetGameObject()!=null);
            Object.DontDestroyOnLoad(_uiRoot.GetGameObject());
        }


        
        
        public static T GetUi<T>() where T: UiActor
        {
            if (_uiDic.ContainsKey(typeof(T)))
            {
                return (T)_uiDic[typeof(T)];
            }
            return null;
        }


        public static async UniTask OpenUi(int index,object[] objs=null)
        {
            if (_typesDic.ContainsKey(index))
            {
                await OpenUi(_typesDic[index]);
            }
        }
        
        public static async UniTask<T> OpenUi<T>(object[] objs=null) where T: UiActor
        {
            return (T)await OpenUi(typeof(T), objs);
        }



        private static List<Type> _loadingUiList = new List<Type>();
        public static async UniTask<UiActor> OpenUi(Type type,object[] objects=null)
        {

            if (_loadingUiList.Contains(type)) return null;
            if (_uiRoot.GetGameObject()==null)
            {
                MyLog.LogError("场景中没有Canvas组件,无法显示Ui物体");
                return null;
            }

            _loadingUiList.Add(type);
            if (_uiDic.ContainsKey(type))
            {
                _uiDic[type].SetActive(true);
                _uiDic[type].Open(objects);
                _uiDic[type].OpenAnim();
                _uiDic[type].transform.SetAsLastSibling();
                if (!_uiList.Contains(_uiDic[type].GetIndex()))
                {
                    _uiList.Add(_uiDic[type].GetIndex());
                }
                _loadingUiList.Remove(type);
                return _uiDic[type];
            }
            
            var uiMode=type.GetCustomAttribute<UiModeAttribute>();
            if (uiMode==null)
            {
                _loadingUiList.Remove(type);
                MyLog.LogError("类不具备UiModeAttribute");
                return null;
            }
           
            Transform tran=GetTransform(uiMode);
            var param = new object[] { tran };
            UiActor obj;
            try
            {
                var ui = Activator.CreateInstance(type,param);
                obj= (UiActor)ui;
                await UniTask.WaitUntil(() => obj.isInit);
            }
            catch (Exception e)
            {
                MyLog.LogError("生成ui失败:"+e.Message);
                _loadingUiList.Remove(type);
                return null;
            }
            
            //var obj =(T)Assembly.GetExecutingAssembly().CreateInstance(t.Namespace+"."+fullName)
            
            obj.Open(objects);
            obj.SetIndex(_index);
            _uiDic.Add(type,obj);
            _uiList.Add(obj.GetIndex());
            _typesDic.Add(obj.GetIndex(),type);
            _index += 1;
            _loadingUiList.Remove(type);
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


        public static void HideAllUi()
        {
            EventMrg.Trigger(MessageType.UiMessage,UiMessageType.Hide,-1);
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
                uiActor.GetGameObject().Destroy();
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
        
        
        public static bool IsCanPlay()
        {
            var nor=GetTransform(new UiModeAttribute(Mode.Normal));
            var count = 0;
            for (int i = 0; i < nor.childCount; i++)
            {
                if (nor.GetChild(i).gameObject.activeSelf)
                {
                    count += 1;
                }
            }
            
            return count<=1;
        }


        // private static ObjectPool<TipsText> _tipstPool=new ObjectPool<TipsText>();
        // public static async UniTask ShowTips(string tips)
        // {
        //     var tipsText=await _tipstPool.DeQueue();
        //     await UniTask.WaitUntil(() => tipsText.GetGameObject() != null);
        //     tipsText.transform.localScale = Vector3.one;
        //     tipsText.SetActive(true);
        //     tipsText.CanvasGroupTipsText.alpha = 1;
        //     tipsText.transform.SetParent(_uiRoot.transform.Find(nameof(Mode.Popup)));
        //     tipsText.transform.localScale = Vector3.one;
        //     var rect = tipsText.transform.GetComponent<RectTransform>();
        //     rect.offsetMin=new Vector2(0,0);
        //     rect.offsetMax=new Vector2(0,0);
        //     Sequence queue = DOTween.Sequence();
        //     tipsText.RectTransformBg.localScale = Vector3.zero;
        //     tipsText.RectTransformBg.anchoredPosition=new Vector2(0, 0);
        //     queue.Append(tipsText.RectTransformBg.DOScale(new Vector3(1, 1, 1), 0.4f));
        //     queue.AppendInterval(0.5f);
        //     queue.Append(tipsText.RectTransformBg.DOLocalMoveY(200, 0.5f));
        //     queue.Play();
        //     tipsText.CanvasGroupTipsText.DOFade(0, 0.5f).SetDelay(0.9f);
        //     tipsText.TextMeshProUGUIText.text = tips;
        //     queue.onComplete += (() =>
        //     {
        //         _tipstPool.EnQueue(tipsText);
        //         tipsText.SetActive(false);
        //     });
        // }
        //
        // public static async UniTask ShowUiFx(string fxName, float time)
        // {
        //     var tran=GetTransform(new UiModeAttribute(Mode.Fx));
        //     var fx = await GameObjectMrg.Instance.Dequeue(fxName, RoleType.Fx);
        //     fx.transform.SetParent(tran);
        //     fx.transform.localPosition = Vector3.zero;
        //     fx.transform.localScale = Vector3.one;
        //     await UniTask.Delay(TimeSpan.FromSeconds(time));
        //     GameObjectMrg.Instance.Enqueue(fx);
        // }
        
    }
}