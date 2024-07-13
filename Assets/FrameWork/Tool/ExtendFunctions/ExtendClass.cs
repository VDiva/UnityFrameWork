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

        public static void SetActive(this UiActor mono, bool active)
        {
            if (mono.GetGameObject().activeSelf!=active)
            {
                mono.GetGameObject().SetActive(active);
            }
        }
    }
}