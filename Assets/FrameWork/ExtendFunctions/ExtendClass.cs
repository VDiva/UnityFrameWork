using UnityEngine;

namespace FrameWork
{
    public static class ExtendClass
    {
        public static void SetActive(this MonoBehaviour mono, bool active)
        {
            mono.gameObject.SetActive(active);
        }
    }
}