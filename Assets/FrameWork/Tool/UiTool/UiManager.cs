using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;


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
        private static UiActor _uiRoot;
        public static void Init()
        {
            if (_uiDic.Count == 0) _index = 0;
        }

        /// <summary>热更新入口传入生成的 UI 根节点，避免 AOT 引用业务程序集。</summary>
        public static void Init(UiActor root)
        {
            ABMrg.BeforeSceneLoading = HideAllUi;
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (_uiRoot != null && !ReferenceEquals(_uiRoot, root))
            {
                foreach (var type in new List<Type>(_uiDic.Keys))
                {
                    try { RemoveUi(type); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            _uiRoot = root;
            _layerDic.Clear();
            Init();
            CheckUiRoot().Forget(Debug.LogException);
        }

        public static async UniTask InitAsync(UiActor root)
        {
            Init(root);
            await root.Initialization;
        }

        static UiManager()
        {
            _uiDic = new Dictionary<Type, UiActor>();
            _typesDic = new Dictionary<int, Type>();
            _uiList = new List<int>();

            
        }

        private static async UniTask CheckUiRoot()
        {
            var root = _uiRoot;
            await root.Initialization;
            if (ReferenceEquals(root, _uiRoot)) Object.DontDestroyOnLoad(root.GetGameObject());
        }


        public static Camera GetCamera()
        {
            return _uiRoot?.GetGameObject()?.GetComponentInChildren<Camera>(true);
        }
        
        
        internal static UiActor GetUiByType(Type type) => _uiDic.TryGetValue(type, out var actor) ? actor : null;

        public static T GetUi<T>() where T : UiActor
        {
            if (!_uiDic.TryGetValue(typeof(T), out var actor)) return null;
            if (actor.GetGameObject() != null) return (T)actor;
            ForgetUi(actor);
            return null;
        }

        public static async UniTask OpenUi(int index,object[] objs=null)
        {
            if (_typesDic.ContainsKey(index))
            {
                await OpenUi(_typesDic[index], objs);
            }
        }
        
        public static async UniTask<T> OpenUi<T>(object[] objs=null) where T: UiActor
        {
            return (T)await OpenUi(typeof(T), objs);
        }



        private static readonly Dictionary<Type, UniTaskCompletionSource<UiActor>> _loadingUi =
            new Dictionary<Type, UniTaskCompletionSource<UiActor>>();

        public static UniTask<UiActor> OpenUi(Type type, object[] objects = null)
        {
            if (type == null || !typeof(UiActor).IsAssignableFrom(type) || type.IsAbstract)
                throw new ArgumentException("必须指定可实例化的 UiActor 类型。", nameof(type));
            if (_loadingUi.TryGetValue(type, out var pending)) return pending.Task;
            var completion = new UniTaskCompletionSource<UiActor>();
            _loadingUi.Add(type, completion);
            OpenUiCore(type, objects, completion).Forget(Debug.LogException);
            return completion.Task;
        }

        private static async UniTask OpenUiCore(Type type, object[] objects,
            UniTaskCompletionSource<UiActor> completion)
        {
            UiActor created = null;
            UiActor result = null;
            try
            {
                var root = _uiRoot ?? throw new InvalidOperationException("请先初始化 UI 根节点。");
                await root.Initialization;
                if (!ReferenceEquals(root, _uiRoot) || root.GetGameObject() == null)
                    throw new InvalidOperationException("UI 根节点已变更或销毁。");
                if (_uiDic.TryGetValue(type, out var existing))
                {
                    if (existing.GetGameObject() != null)
                    {
                        existing.SetActive(true);
                        existing.Open(objects);
                        existing.OpenAnim();
                        existing.transform.SetAsLastSibling();
                        _uiList.Remove(existing.GetIndex());
                        _uiList.Add(existing.GetIndex());
                        result = existing;
                        return;
                    }
                    ForgetUi(existing);
                }
                var mode = type.GetCustomAttribute<UiModeAttribute>() ??
                    throw new InvalidOperationException(type.Name + " 缺少 UiModeAttribute");
                created = (UiActor)Activator.CreateInstance(type, new object[] { GetTransform(mode) });
                await created.Initialization;
                if (!ReferenceEquals(root, _uiRoot) || root.GetGameObject() == null)
                    throw new InvalidOperationException("UI 根节点已变更或销毁。");
                created.SetIndex(_index++);
                created.Open(objects);
                if (created.GetGameObject() == null) throw new InvalidOperationException("UI 在 Open 期间已销毁。");
                _uiDic.Add(type, created);
                _typesDic.Add(created.GetIndex(), type);
                _uiList.Add(created.GetIndex());
                result = created;
            }
            catch (Exception error)
            {
                if (created != null && created.GetGameObject() != null)
                    ABMrg.ReleaseInstantiate(created.GetGameObject());
                Debug.LogException(error);
            }
            finally
            {
                _loadingUi.Remove(type);
                completion.TrySetResult(result); // 保留原有打开失败返回 null 的约定。
            }
        }

        internal static void ForgetUi(UiActor actor)
        {
            var type = actor.GetType();
            if (_uiDic.TryGetValue(type, out var current) && ReferenceEquals(current, actor))
            {
                _uiDic.Remove(type);
                _typesDic.Remove(actor.GetIndex());
                _uiList.Remove(actor.GetIndex());
            }
        }

        public static bool IsOpenUi<T>()
        {
            if (!_uiDic.TryGetValue(typeof(T), out var actor)) return false;
            if (actor.GetGameObject() != null) return true;
            ForgetUi(actor);
            return false;
        }

        public static bool IsShowUi<T>()
        {
            if (_uiDic.ContainsKey(typeof(T)))
            {
                var go = _uiDic[typeof(T)].GetGameObject();
                return go != null && go.activeSelf;
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
                _uiList.Remove(uiActor.GetIndex());
                try { uiActor.OnClose(); }
                finally
                {
                    if (uiActor.GetGameObject() != null) uiActor.SetActive(false);
                }
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
                ForgetUi(uiActor);
                try { uiActor.OnClose(); }
                finally { ABMrg.ReleaseInstantiate(uiActor.GetGameObject()); }
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


        // 提示样式和特效对象池由接入项目提供。
        public static Func<string, UniTask> TipsHandler;
        public static Func<string, float, UniTask> UiFxHandler;
        public static UniTask ShowTips(string tips)
        {
            if (TipsHandler == null) throw new InvalidOperationException("请先设置 UiManager.TipsHandler");
            return TipsHandler(tips);
        }
        public static UniTask ShowUiFx(string fxName, float time)
        {
            if (UiFxHandler == null) throw new InvalidOperationException("请先设置 UiManager.UiFxHandler");
            return UiFxHandler(fxName, time);
        }
    }
}
