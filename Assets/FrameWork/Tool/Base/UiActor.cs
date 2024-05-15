using System.Collections.Generic;
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
            
            Registered((int)MessageType.UiMessage,(int)UiMessageType.Show,ShowUi);
            Registered((int)MessageType.UiMessage,(int)UiMessageType.Hide,HideUi);
            Registered((int)MessageType.UiMessage,(int)UiMessageType.Remove,RemoveUi);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            Unbinding((int)MessageType.UiMessage,(int)UiMessageType.Show,ShowUi);
            Unbinding((int)MessageType.UiMessage,(int)UiMessageType.Hide,HideUi);
            Unbinding((int)MessageType.UiMessage,(int)UiMessageType.Remove,RemoveUi);
           
        }

        private void ShowUi(List<object> parma)
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

        private void HideUi(List<object> parma)
        {
            HideUi((int)parma[0]);
        }
        private void HideUi(int index)
        {
            if (index.Equals(GetIndex())|| index==-1)
            {
                GetGameObject().SetActive(false);
            }
        }

        private void RemoveUi(List<object> parma)
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