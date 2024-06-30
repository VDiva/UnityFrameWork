using UnityEngine;

namespace FrameWork
{
    public class SingletonAsMono<T> : MonoBehaviour where T: Component
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance==null)
                {

                    if (FindObjectOfType<T>()!=null)
                    {
                        _instance = FindObjectOfType<T>();
                    }
                    else
                    {
                        GameObject go = new GameObject();
                        go.name = typeof(T).Name;
                        _instance=go.AddComponent<T>();
                    }
                    
                    DontDestroyOnLoad(_instance);
                }

                return _instance;
            }
        }

        
    }
        
    
}