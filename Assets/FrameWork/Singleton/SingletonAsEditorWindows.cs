using System;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Singleton
{
    public class SingletonAsEditorWindows<T>: EditorWindow where T: EditorWindow
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance==null)
                {
                    _instance = GetWindow<T>();
                }

                return _instance;
            }
        }
        
    }
}