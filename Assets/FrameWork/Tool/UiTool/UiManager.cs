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
        /// <summary>
        /// 默认 UI 隐藏后自动销毁并卸载内存的超时秒数（采用忽略 timeScale 的真实时间）。
        /// 设为小于 0 可关闭全局自动销毁；设为 0 表示隐藏后立即销毁。
        /// 单个 UI 可通过 <see cref="UiModeAttribute.Permanent"/> 或 <see cref="UiModeAttribute.AutoDestroyDelaySeconds"/> 覆盖。
        /// </summary>
        public static float DefaultAutoDestroyDelaySeconds = 60f;

        /// <summary>自动销毁超时 UI 后是否触发 YooAsset 未引用资源卸载（按帧合并防抖）。</summary>
        public static bool AutoUnloadMemoryOnAutoDestroy = true;

        /// <summary>自动卸载内存时是否同时调用 Unity <see cref="Resources.UnloadUnusedAssets"/>。</summary>
        public static bool AutoUnloadUnityResourcesOnAutoDestroy = false;

        private static int _index;
        private static readonly Dictionary<Type, UiActor> _uiDic = new Dictionary<Type, UiActor>();
        private static readonly Dictionary<int, Type> _typesDic = new Dictionary<int, Type>();
        private static readonly List<int> _uiList = new List<int>();
        private static readonly Dictionary<Type, UiModeAttribute> _modeCache = new Dictionary<Type, UiModeAttribute>();
        private static readonly Dictionary<Type, Func<Transform, UiActor>> _factoryCache = new Dictionary<Type, Func<Transform, UiActor>>();
        private static readonly Dictionary<Type, TimeData> _autoDestroyTimers = new Dictionary<Type, TimeData>();
        private static readonly Type[] _ctorParamTypes = { typeof(Transform) };
        private static readonly object[] _ctorArgScratch = new object[1];
        private static UiActor _uiRoot;
        private static Camera _cachedCamera;
        private static bool _uiMessagesSubscribed;
        private static bool _unloadMemoryScheduled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CancelAllAutoDestroyTimers();
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
            _uiMessagesSubscribed = false;
            _unloadMemoryScheduled = false;
            DefaultAutoDestroyDelaySeconds = 60f;
            AutoUnloadMemoryOnAutoDestroy = true;
            AutoUnloadUnityResourcesOnAutoDestroy = false;
            TipsHandler = null;
            UiFxHandler = null;
        }

        public static void Init()
        {
            if (_uiDic.Count == 0) _index = 0;
            EnsureUiMessagesSubscribed();
        }

        private static void EnsureUiMessagesSubscribed()
        {
            if (_uiMessagesSubscribed) return;
            _uiMessagesSubscribed = true;
            EventMrg.Subscribe<int>((int)MessageType.UiMessage, (int)UiMessageType.Show, OnShowUiMessage);
            EventMrg.Subscribe<int>((int)MessageType.UiMessage, (int)UiMessageType.Hide, OnHideUiMessage);
            EventMrg.Subscribe<int>((int)MessageType.UiMessage, (int)UiMessageType.Remove, OnRemoveUiMessage);
        }

        private static void OnShowUiMessage(int index)
        {
            if (index >= 0)
                OpenUi(index).Forget(Debug.LogException);
        }

        private static void OnHideUiMessage(int index)
        {
            if (index == -1)
                HideAllUi();
            else
                HideUi(index);
        }

        private static void OnRemoveUiMessage(int index)
        {
            if (index == -1)
                RemoveAllUi();
            else
                RemoveUi(index);
        }

        /// <summary>热更新入口传入生成的 UI 根节点，避免 AOT 引用业务程序集。</summary>
        public static void Init(UiActor root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            ABMrg.BeforeSceneLoading -= HideAllUi;
            ABMrg.BeforeSceneLoading += HideAllUi;
            if (_uiRoot != null && !ReferenceEquals(_uiRoot, root))
            {
                CancelAllAutoDestroyTimers();
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

        public static async UniTask OpenUi(int index, object[] objs = null)
        {
            if (_typesDic.TryGetValue(index, out var type))
            {
                await OpenUi(type, objs);
            }
        }

        public static async UniTask<T> OpenUi<T>(object[] objs = null) where T : UiActor
        {
            return (T)await OpenUi(typeof(T), objs);
        }

        private static readonly Dictionary<Type, UniTaskCompletionSource<UiActor>> _loadingUi =
            new Dictionary<Type, UniTaskCompletionSource<UiActor>>();

        private static bool IsCurrentOpen(Type type, UniTaskCompletionSource<UiActor> completion) =>
            _loadingUi.TryGetValue(type, out var current) && ReferenceEquals(current, completion);

        private static UiModeAttribute GetModeAttribute(Type type)
        {
            if (!_modeCache.TryGetValue(type, out var mode))
            {
                mode = type.GetCustomAttribute<UiModeAttribute>();
                if (mode != null) _modeCache[type] = mode;
            }
            return mode;
        }

        private static Func<Transform, UiActor> BuildUiFactory(Type type)
        {
            var ctor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                _ctorParamTypes,
                null);
            if (ctor == null)
                throw new InvalidOperationException(type.Name + " 缺少接收 Transform 参数的构造函数。");

#if !ENABLE_IL2CPP && !UNITY_WEBGL
            try
            {
                var param = Expression.Parameter(typeof(Transform), "parent");
                var body = Expression.Convert(Expression.New(ctor, param), typeof(UiActor));
                return Expression.Lambda<Func<Transform, UiActor>>(body, param).Compile();
            }
            catch
            {
                // 回退到缓存 ConstructorInfo 调用。
            }
#endif
            // IL2CPP / WebGL 等 AOT 平台直接使用已缓存的 ConstructorInfo 与复用参数槽，避免首次打开抛异常。
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

        private static UiActor GetLiveUi(Type type)
        {
            if (!_uiDic.TryGetValue(type, out var actor)) return null;
            if (actor.GetGameObject() != null) return actor;
            ForgetUi(actor);
            return null;
        }

        private static void CancelAutoDestroyTimer(Type type)
        {
            if (type == null) return;
            if (_autoDestroyTimers.TryGetValue(type, out var timer))
            {
                _autoDestroyTimers.Remove(type);
                Timer.DestroyTimer(timer);
            }
        }

        private static void CancelAllAutoDestroyTimers()
        {
            if (_autoDestroyTimers.Count == 0) return;
            foreach (var timer in _autoDestroyTimers.Values)
                Timer.DestroyTimer(timer);
            _autoDestroyTimers.Clear();
        }

        private static float ResolveAutoDestroyDelay(Type type)
        {
            var mode = GetModeAttribute(type);
            if (mode != null)
            {
                if (mode.Permanent) return -1f;
                if (mode.AutoDestroyDelaySeconds >= 0f) return mode.AutoDestroyDelaySeconds;
            }
            return DefaultAutoDestroyDelaySeconds;
        }

        private static void ScheduleAutoDestroy(Type type)
        {
            CancelAutoDestroyTimer(type);
            float delay = ResolveAutoDestroyDelay(type);
            if (delay < 0f || float.IsNaN(delay) || float.IsInfinity(delay))
                return;

            if (delay == 0f)
            {
                AutoDestroyExpiredUi(type);
                return;
            }

            var timer = Timer.DelayCall(delay, () => AutoDestroyExpiredUi(type), ignoreTimeScale: true);
            _autoDestroyTimers[type] = timer;
        }

        private static void AutoDestroyExpiredUi(Type type)
        {
            _autoDestroyTimers.Remove(type);
            if (_loadingUi.ContainsKey(type)) return;
            var uiActor = GetLiveUi(type);
            if (uiActor == null) return;
            var go = uiActor.GetGameObject();
            if (go != null && go.activeSelf) return; // 期间已被重新打开

            // 隐藏时已触发过 OnClose，此处直接释放对象与句柄，避免重复调用 OnClose。
            RemoveUiInternal(type, invokeOnClose: false);
            if (AutoUnloadMemoryOnAutoDestroy)
                ScheduleMemoryUnload();
        }

        private static void ScheduleMemoryUnload()
        {
            if (_unloadMemoryScheduled) return;
            _unloadMemoryScheduled = true;
            UnloadMemoryNextFrameAsync().Forget(Debug.LogException);
        }

        private static async UniTask UnloadMemoryNextFrameAsync()
        {
            try
            {
                // Object.Destroy 在帧末执行 OnDestroy 释放 YooInstanceLease 句柄，等待下一帧再卸载未使用资源包。
                await UniTask.NextFrame();
                _unloadMemoryScheduled = false;
                await ABMrg.UnloadUnusedAssetsAsync(AutoUnloadUnityResourcesOnAutoDestroy);
            }
            catch
            {
                _unloadMemoryScheduled = false;
                throw;
            }
        }

        public static UniTask<UiActor> OpenUi(Type type, object[] objects = null)
        {
            if (type == null || !typeof(UiActor).IsAssignableFrom(type) || type.IsAbstract)
                throw new ArgumentException("必须指定可实例化的 UiActor 类型。", nameof(type));
            CancelAutoDestroyTimer(type);
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
                var mode = GetModeAttribute(type);
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
            CancelAutoDestroyTimer(type);
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
                bool wasActive = uiActor.GetGameObject().activeSelf;
                try
                {
                    if (wasActive) uiActor.OnClose();
                }
                finally
                {
                    if (uiActor.GetGameObject() != null) uiActor.SetActive(false);
                }
                if (GetLiveUi(type) != null)
                    ScheduleAutoDestroy(type);
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
            if (_uiDic.Count == 0) return;
            var types = new Type[_uiDic.Count];
            _uiDic.Keys.CopyTo(types, 0);
            for (int i = 0; i < types.Length; i++)
            {
                try { HideUi(types[i]); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        public static void RemoveAllUi(bool unloadMemory = false)
        {
            CancelAllAutoDestroyTimers();
            _loadingUi.Clear();
            if (_uiDic.Count > 0)
            {
                var types = new Type[_uiDic.Count];
                _uiDic.Keys.CopyTo(types, 0);
                for (int i = 0; i < types.Length; i++)
                {
                    try { RemoveUi(types[i]); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            if (unloadMemory)
                ScheduleMemoryUnload();
        }

        public static void RemoveUi<T>() where T : UiActor
        {
            RemoveUi(typeof(T));
        }

        public static void RemoveUi(Type type)
        {
            RemoveUiInternal(type, invokeOnClose: true);
        }

        private static void RemoveUiInternal(Type type, bool invokeOnClose)
        {
            CancelAutoDestroyTimer(type);
            _loadingUi.Remove(type);
            var uiActor = GetLiveUi(type);
            if (uiActor != null)
            {
                bool shouldClose = invokeOnClose && uiActor.GetGameObject().activeSelf;
                ForgetUi(uiActor);
                try
                {
                    if (shouldClose) uiActor.OnClose();
                }
                finally
                {
                    ABMrg.ReleaseInstantiate(uiActor.GetGameObject());
                }
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

            return count <= 1;
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
