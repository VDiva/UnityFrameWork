using UnityEngine;
namespace FrameWork
{
    // 保留当前项目的获取/创建方式；登录和存档改由业务层组合实现。
    public class SingletonAsMono<T> : MonoBehaviour where T : Component
    {
        private static T _instance;
        private static bool _isQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
        }

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (_isQuitting) return null;
                    _instance = FindObjectOfType<T>();
                    if (_instance == null) _instance = new GameObject(typeof(T).Name).AddComponent<T>();
                    if (Application.isPlaying) DontDestroyOnLoad(_instance);
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
