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
            
            Registered(MessageType.UiMessage,UiMessageType.Show,ShowUi);
            Registered(MessageType.UiMessage,UiMessageType.Hide,HideUi);
            Registered(MessageType.UiMessage,UiMessageType.Remove,RemoveUi);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            Unbinding(MessageType.UiMessage,UiMessageType.Show,ShowUi);
            Unbinding(MessageType.UiMessage,UiMessageType.Hide,HideUi);
            Unbinding(MessageType.UiMessage,UiMessageType.Remove,RemoveUi);
           
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