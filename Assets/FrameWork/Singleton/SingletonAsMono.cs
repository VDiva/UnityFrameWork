using UnityEngine;

namespace FrameWork.Singleton
{
    public class SingletonAsMono<T> : MonoBehaviour where T: class
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance==null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent(typeof(T)) as T;
                }

                return _instance;
            }
        }
        
    }
}