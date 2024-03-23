using UnityEngine;

namespace FrameWork
{
    public class UiActor: Actor
    {
        protected UiActor(): base() {}
        protected UiActor(Transform trans): base(trans) {}
        
        public override void Start()
        {
            base.Start();
            // UiManager.Instance.ShowUiAction += ShowUi;
            // UiManager.Instance.HideUiAction += HideUi;
            // UiManager.Instance.RemoveUiAction += RemoveUi;
            EventManager.AddListener(MessageType.UiMessage,UiMessageType.Show,ShowUi);
            EventManager.AddListener(MessageType.UiMessage,UiMessageType.Hide,HideUi);
            EventManager.AddListener(MessageType.UiMessage,UiMessageType.Remove,RemoveUi);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            // UiManager.Instance.ShowUiAction -= ShowUi;
            // UiManager.Instance.HideUiAction -= HideUi;
            // UiManager.Instance.RemoveUiAction -= RemoveUi;
            
            EventManager.RemoveListener(MessageType.UiMessage,UiMessageType.Show,ShowUi);
            EventManager.RemoveListener(MessageType.UiMessage,UiMessageType.Hide,HideUi);
            EventManager.RemoveListener(MessageType.UiMessage,UiMessageType.Remove,RemoveUi);
        }

        private void ShowUi(object[] parma)
        {
            ShowUi((int)parma[0]);
        }

        private void ShowUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                GetGameObject().SetActive(true);
            }
        }

        private void HideUi(object[] parma)
        {
            HideUi((int)parma[0]);
        }
        private void HideUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                GetGameObject().SetActive(false);
            }
        }

        private void RemoveUi(object[] parma)
        {
            RemoveUi((int)parma[0]);
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