using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FrameWork
{
    /// <summary>
    /// 轻量日志封装：普通 Log 在非 Editor/Debug/FRAMEWORK_LOG 宏下自动编译期裁剪，避免正式包字符串拼接与 GC。
    /// </summary>
    public static class MyLog
    {
        public static bool EnableLog = true;
        public static bool EnableWarning = true;
        public static bool EnableError = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EnableLog = true;
            EnableWarning = true;
            EnableError = true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("FRAMEWORK_LOG"), Conditional("Debug"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string info)
        {
            if (!EnableLog) return;
            Debug.Log(info);
        }

        public static void LogWarning(string info)
        {
            if (!EnableWarning) return;
            Debug.LogWarning(info);
        }

        public static void LogError(string info)
        {
            if (!EnableError) return;
            Debug.LogError(info);
        }
    }
}
