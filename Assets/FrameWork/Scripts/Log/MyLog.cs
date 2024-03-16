using UnityEngine;

namespace FrameWork
{
    public static class MyLog
    {
        public static void Log(string info)
        {
           // if (!GlobalVariables.Configure.EnableLog)return;
            Debug.Log(info);
        }
        
        public static void LogWarning(string info)
        {
            //if (!GlobalVariables.Configure.EnableLog)return;
            Debug.LogWarning(info);
        }
        
        public static void LogError(string info)
        {
            //if (!GlobalVariables.Configure.EnableLog)return;
            Debug.LogError(info);
        }
    }
}