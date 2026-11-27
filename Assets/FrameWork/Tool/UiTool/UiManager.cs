using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Cysharp.Threading.Tasks;


using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork
{
    public static class UiManager 
    {
        
        private static int _index;
        private static readonly Dictionary<Type, UiActor> _uiDic = new Dictionary<Type, UiActor>();
        private static readonly Dictionary<int, Type> _typesDic = new Dictionary<int, Type>();
        private static readonly List<int> _uiList = new List<int>();
        private static readonly Dictionary<Type, UiModeAttribute> _modeCache = new Dictionary<Type, UiModeAttribute>();
        private static readonly Dictionary<Type, Func<Transform, UiActor>> _factoryCache = new Dictionary<Type, Func<Transform, UiActor>>();
        private static readonly Type[] _ctorParamTypes = { typeof(Transform) };
        private static readonly object[] _ctorArgScratch = new object[1];
        private static UiActor _uiRoot;
        private static Camera _cachedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _index = 0;
            _uiDic.Clear();
            _typesDic.Clear();
            _uiList.Clear();
            _layerDic.Clear();
            _loadingUi.Clear();
            _modeCache.Clear();
            _factoryCache.Clear();
            _ctorArgScratch[0] = null;
            _uiRoot = null;
            _cachedCamera = null;
            TipsHandler = null;
            UiFxHandler = null;
        }

        public static void Init()
        {
            if (_uiDic.Count == 0) _index = 0;
        }

        /// <summary>热更新入口传入生成的 UI 根节点，避免 AOT 引用业务程序集。</summary>
        public static void Init(UiActor root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            ABMrg.BeforeSceneLoading -= HideAllUi;
            ABMrg.BeforeSceneLoading += HideAllUi;
            if (_uiRoot != null && !ReferenceEquals(_uiRoot, root))
            {
                _loadingUi.Clear();
                foreach (var type in new List<Type>(_uiDic.Keys))
                {
                    try { RemoveUi(type); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            _uiRoot = root;
            _cachedCamera = null;
            _layerDic.Clear();
            Init();
            CheckUiRoot().Forget(Debug.LogException);
        }

        public static async UniTask InitAsync(UiActor root)
        {
            Init(root);
            await root.Initialization;
        }

        private static async UniTask CheckUiRoot()
        {
            var root = _uiRoot;
            await root.Initialization;
            if (ReferenceEquals(root, _uiRoot)) Object.DontDestroyOnLoad(root.GetGameObject());
        }


        public static Camera GetCamera()
        {
            if (_cachedCamera != null) return _cachedCamera;
            _cachedCamera = _uiRoot?.GetGameObject()?.GetComponentInChildren<Camera>(true);
            return _cachedCamera;
        }
        
        
        internal static UiActor GetUiByType(Type type) => _uiDic.TryGetValue(type, out var actor) ? actor : null;

        public static T GetUi<T>() where T : UiActor
        {
            return (T)GetLiveUi(typeof(T));
        }

        public static async UniTask OpenUi(int index,object[] objs=null)
        {
            if (_typesDic.TryGetValue(index, out var type))
            {
                await OpenUi(type, objs);
            }
        }
        
        public static async UniTask<T> OpenUi<T>(object[] objs=null) where T: UiActor
        {
            return (T)await OpenUi(typeof(T), objs);
        }



        private static readonly Dictionary<Type, UniTaskCompletionSource<UiActor>> _loadingUi =
            new Dictionary<Type, UniTaskCompletionSource<UiActor>>();

        private static bool IsCurrentOpen(Type type, UniTaskCompletionSource<UiActor> completion) =>
            _loadingUi.TryGetValue(type, out var current) && ReferenceEquals(current, completion);

        private static Func<Transform, UiActor> BuildUiFactory(Type type)
        {
            var ctor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                _ctorParamTypes,
                null);
            if (ctor == null)
                throw new InvalidOperationException(type.Name + " 缺少接收 Transform 参数的构造函数。");

            try
            {
                var param = Expression.Parameter(typeof(Transform), "parent");
                var body = Expression.Convert(Expression.New(ctor, param), typeof(UiActor));
                return Expression.Lambda<Func<Transform, UiActor>>(body, param).Compile();
            }
            catch
            {
                // AOT / 受限平台回退：复用已缓存的 ConstructorInfo 与单元素参数槽，仍免去每次反射查找构造函数与数组分配。
                return parent =>
                {
                    _ctorArgScratch[0] = parent;
                    try
                    {
                        return (UiActor)ctor.Invoke(_ctorArgScratch);
                    }
                    finally
                    {
                        _ctorArgScratch[0] = null;
                    }
                };
            }
        }

        private static UiActor GetLiveUi(Type type)
        {
            if (!_uiDic.TryGetValue(type, out var actor)) return null;
            if (actor.GetGameObject() != null) return actor;
            ForgetUi(actor);
            return null;
        }

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
                if (!IsCurrentOpen(type, completion)) return;
                if (!ReferenceEquals(root, _uiRoot) || root.GetGameObject() == null)
                    throw new InvalidOperationException("UI 根节点已变更或销毁。");
                var existing = GetLiveUi(type);
                if (existing != null)
                {
                    existing.SetActive(true);
                    if (!IsCurrentOpen(type, completion) || existing.GetGameObject() == null) return;
                    existing.Open(objects);
                    if (!IsCurrentOpen(type, completion) || existing.GetGameObject() == null) return;
                    existing.OpenAnim();
                    if (!IsCurrentOpen(type, completion) || existing.GetGameObject() == null) return;
                    existing.transform.SetAsLastSibling();
                    _uiList.Remove(existing.GetIndex());
                    _uiList.Add(existing.GetIndex());
                    result = existing;
                    return;
                }
                if (!_modeCache.TryGetValue(type, out var mode))
                {
                    mode = type.GetCustomAttribute<UiModeAttribute>();
                    if (mode != null) _modeCache[type] = mode;
                }
                if (mode == null)
                    throw new InvalidOperationException(type.Name + " 缺少 UiModeAttribute");
                if (!_factoryCache.TryGetValue(type, out var factory))
                {
                    factory = BuildUiFactory(type);
                    _factoryCache[type] = factory;
                }
                created = factory(GetTransform(mode));
                await created.Initialization;
                if (!IsCurrentOpen(type, completion)) return;
                if (!ReferenceEquals(root, _uiRoot) || root.GetGameObject() == null)
                    throw new InvalidOperationException("UI 根节点已变更或销毁。");
                created.SetIndex(_index++);
                created.Open(objects);
                if (!IsCurrentOpen(type, completion)) return;
                if (created.GetGameObject() == null) throw new InvalidOperationException("UI 在 Open 期间已销毁。");
                _uiDic.Add(type, created);
                _typesDic.Add(created.GetIndex(), type);
                _uiList.Add(created.GetIndex());
                result = created;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                try
                {
                    if (created != null && result == null)
                        ABMrg.ReleaseInstantiate(created.GetGameObject());
                }
                finally
                {
                    if (IsCurrentOpen(type, completion)) _loadingUi.Remove(type);
                    completion.TrySetResult(result); // 保留原有打开失败返回 null 的约定。
                }
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

        public static bool IsOpenUi<T>() => GetLiveUi(typeof(T)) != null;

        public static bool IsShowUi<T>()
        {
            return GetLiveUi(typeof(T))?.GetGameObject().activeSelf == true;
        }

        public static T HideUi<T>() where T : UiActor
        {
            return (T)HideUi(typeof(T));
        }
        
        
        public static UiActor HideUi(Type type) 
        {
            _loadingUi.Remove(type);
            var uiActor = GetLiveUi(type);
            if (uiActor != null)
            {
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
            if (_typesDic.TryGetValue(index, out var type))
            {
                HideUi(type);
            }
        }


        public static void HideAllUi()
        {
            _loadingUi.Clear();
            EventMrg.Trigger((int)MessageType.UiMessage, (int)UiMessageType.Hide, -1);
        }

        public static void RemoveUi<T>() where T: UiActor
        {
            RemoveUi(typeof(T));
        }
        
        public static void RemoveUi(Type type)
        {
            _loadingUi.Remove(type);
            var uiActor = GetLiveUi(type);
            if (uiActor != null)
            {
                ForgetUi(uiActor);
                try { uiActor.OnClose(); }
                finally { ABMrg.ReleaseInstantiate(uiActor.GetGameObject()); }
            }
            
        }
        
        public static void RemoveUi(int index)
        {
            if (_typesDic.TryGetValue(index, out var type))
            {
                RemoveUi(type);
            }
        }



        public static void Back()
        {
            while (_uiList.Count > 0)
            {
                int last = _uiList.Count - 1;
                int index = _uiList[last];
                _uiList.RemoveAt(last);
                if (_typesDic.TryGetValue(index, out var type) && HideUi(type) != null)
                    return;
            }
        }

        private static readonly Dictionary<Mode, Transform> _layerDic = new Dictionary<Mode, Transform>();
        public static Transform GetTransform(UiModeAttribute uiModeAttribute) =>
            GetTransform(uiModeAttribute.Mode);

        public static Transform GetTransform(Mode mode)
        {
            if (_layerDic.TryGetValue(mode, out var layer) && layer != null)
                return layer;

            var root = _uiRoot?.GetGameObject();
            if (root == null)
                throw new InvalidOperationException("请先初始化 UI 根节点。");
            var rootTransform = root.transform;
            string layerName = mode.ToString();
            layer = rootTransform.Find(layerName);
            if (layer == null)
            {
                if (rootTransform.childCount == 0)
                    throw new InvalidOperationException("UI 根节点缺少用于创建层级的模板子节点。");
                layer = Object.Instantiate(rootTransform.GetChild(0), rootTransform);
                layer.name = layerName;
            }
            _layerDic[mode] = layer;
            return layer;
        }
        
        
        public static bool IsCanPlay()
        {
            var nor = GetTransform(Mode.Normal);
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
