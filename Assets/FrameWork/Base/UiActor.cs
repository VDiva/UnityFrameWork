using UnityEngine;

namespace FrameWork
{
    public class UiActor: Actor
    {
        public override void Start()
        {
            base.Start();
            UiManager.Instance.ShowUiAction += ShowUi;
            UiManager.Instance.HideUiAction += HideUi;
            UiManager.Instance.RemoveUiAction += RemoveUi;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            UiManager.Instance.ShowUiAction -= ShowUi;
            UiManager.Instance.HideUiAction -= HideUi;
            UiManager.Instance.RemoveUiAction -= RemoveUi;
        }

        private void ShowUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                GetGameObject().SetActive(true);
            }
        }

        private void HideUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                GetGameObject().SetActive(false);
            }
        }

        private void RemoveUi(int index)
        {
            if (index.Equals(GetIndex())||index==-1)
            {
                GameObject.Destroy(GetGameObject());
            }
        }
    }
}