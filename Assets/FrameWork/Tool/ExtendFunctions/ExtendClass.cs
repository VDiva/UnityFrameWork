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

        public static List<XNode.Node> GetOutNode(this XNode.NodePort node)
        {
            return node.GetConnections().Select((port => port.node)).ToList();
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
            Tool.HideAllChild(tran);
            
            for (int i = 0; i < count; i++)
            {
                Transform tf = null;
                if (tran.childCount>i)
                {
                    tf = tran.GetChild(i);
                }
                else
                {
                    tf = GameObject.Instantiate(go, tran);
                }
                tf.SetActive(true);
                action?.Invoke(i,tf.gameObject);
            }
        }
    }
}