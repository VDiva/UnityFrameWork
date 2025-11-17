using System.Collections.Generic;
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
    }
}