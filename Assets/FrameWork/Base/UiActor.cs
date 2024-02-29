using System;
using UnityEngine;

namespace FrameWork
{
    public class UiActor : Actor
    {
        protected virtual void Start()
        {
            UiManager.Instance.ShowUiAction += ShowUi;
            UiManager.Instance.HideUiAction += HideUi;
            UiManager.Instance.RemoveUiAction += RemoveUi;
        }

        protected virtual void OnDestroy()
        {
            UiManager.Instance.ShowUiAction -= ShowUi;
            UiManager.Instance.HideUiAction -= HideUi;
            UiManager.Instance.RemoveUiAction -= RemoveUi;
        }

        private void ShowUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                gameObject.SetActive(true);
            }
        }
        
        private void HideUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                gameObject.SetActive(false);
            }
        }

        private void RemoveUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                Destroy(gameObject);
                UiManager.Instance.RemoveSu(GetActorName());
            }
        }
    }
}