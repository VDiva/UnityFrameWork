using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class UiActor: Actor
    {
        protected UiActor(): base() {}
        protected UiActor(Transform trans): base(trans) {}


        public virtual void Open(object[] objects)
        {
            Time.timeScale = 0;
        }
        
        public override void Start()
        {
            base.Start();
            
            AddListener((int)MessageType.UiMessage,(int)UiMessageType.Show,ShowUi);
            AddListener((int)MessageType.UiMessage,(int)UiMessageType.Hide,HideUi);
            AddListener((int)MessageType.UiMessage,(int)UiMessageType.Remove,RemoveUi);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            RemoveListener((int)MessageType.UiMessage,(int)UiMessageType.Show,ShowUi);
            RemoveListener((int)MessageType.UiMessage,(int)UiMessageType.Hide,HideUi);
            RemoveListener((int)MessageType.UiMessage,(int)UiMessageType.Remove,RemoveUi);
           
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
        
        public void CloseUi()
        {
            UiManager.HideUi(GetIndex());
        }
        
        public virtual void OnClose()
        {
        }
        
    }
}