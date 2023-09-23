using UnityEngine;

namespace FrameWork.ExtendFunctions
{
    public static class ExtendClass
    {
        public static void SetActive(this MonoBehaviour mono, bool active)
        {
            mono.gameObject.SetActive(active);
        }
    }
}