using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace FrameWork
{
    public static class ExtendClass
    {
        public static void SetActive(this MonoBehaviour mono, bool active)
        {
            if (mono.gameObject.activeSelf!=active)
            {
                mono.gameObject.SetActive(active);
            }
        }
        
        public static void SetActive(this Component mono, bool active)
        {
            if (mono.gameObject.activeSelf!=active)
            {
                mono.gameObject.SetActive(active);
            }
        }

        public static void SetActive(this Actor mono, bool active)
        {
            if (mono.GetGameObject().activeSelf!=active)
            {
                mono.GetGameObject().SetActive(active);
            }
        }
        
        public static void SetActive(this Transform mono, bool active)
        {
            if (mono.gameObject.activeSelf!=active)
            {
                mono.gameObject.SetActive(active);
            }
        }
        
        public static void SetActiveAsCheck(this GameObject mono, bool active)
        {
            if (mono.activeSelf!=active)
            {
                mono.SetActive(active);
            }
        }


        
        public static void HideChild(this Transform tran,int count)
        {
            for (int i = 0; i < tran.childCount; i++)
            {
                if (i>=count)
                {
                    tran.GetChild(i).gameObject.SetActiveAsCheck(false);
                }
            }
        }


        public static void Destroy(this Transform tran)
        {
            GameObject.Destroy(tran.gameObject);
        }
        
        public static void Destroy(this MonoBehaviour tran)
        {
            GameObject.Destroy(tran.gameObject);
        }
        
        public static void Destroy(this Actor tran)
        {
            GameObject.Destroy(tran.GetGameObject());
        }
        
        public static void Destroy(this GameObject tran)
        {
            GameObject.Destroy(tran);
        }
        
        public static int ToInt(this string v)
        {
            return int.Parse(v);
        }
        
        public static long ToLong(this string v)
        {
            return long.Parse(v);
        }
        
        public static bool ToBool(this string v)
        {
            return bool.Parse(v);
        }
        
        public static T ToEnum<T>(this string v) where T : struct, Enum
        {
            return Enum.Parse<T>(v);
        }
        
        public static float ToFloat(this string v)
        {
            return float.Parse(v,CultureInfo.InvariantCulture);
        }
        
        public static void TranFor(this Transform tran, int count, Transform go, Action<int, GameObject> action = null)
        {
            if (tran == null)
                throw new ArgumentNullException(nameof(tran));

            count = Mathf.Max(0, count);

            // 只创建不足的部分，已有子物体保持当前状态，避免每次刷新都触发
            // OnDisable -> OnEnable。
            while (tran.childCount < count)
            {
                if (go == null)
                    throw new ArgumentNullException(nameof(go), "子物体数量不足时必须提供实例化模板");
                GameObject.Instantiate(go, tran);
            }

            // 仅切换实际需要改变的子物体。
            for (int i = 0; i < tran.childCount; i++)
            {
                Transform child = tran.GetChild(i);
                bool shouldActive = i < count;
                child.SetActive(shouldActive);

                if (shouldActive)
                    action?.Invoke(i, child.gameObject);
            }
        }
    }
}
