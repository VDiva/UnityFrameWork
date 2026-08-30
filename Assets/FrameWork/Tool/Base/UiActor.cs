using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FrameWork
{
    public class UiActor: Actor
    {
        protected Tweener animTweener;
        protected Tweener viewCanvasTweener;
        protected Tweener bgTweener;
        protected UiActor(): base() {}
        protected UiActor(Transform trans): base(trans) {}


        public virtual void Open(object[] objects)
        {
            //Time.timeScale = 0;
            //OpenAnim();
        }

        public virtual void OpenAnim()
        {
            
            var bg=transform.Find("Bg");
            if (bg!=null)
            {
                var canvasGroup=bg.GetComponent<CanvasGroup>();
                if (canvasGroup!=null)
                {
                    bgTweener?.Kill();
                    canvasGroup.alpha = 0;
                    bgTweener=canvasGroup.DOFade(1, 0.3f);  
                }
            }
            
            var view = transform.Find("View");
            if (view != null)
            {
                animTweener?.Kill();
                viewCanvasTweener?.Kill();
                view.transform.localScale=new Vector3(0.4f,0.4f,0.4f);
                animTweener=view.DOScale(new Vector3(1,1,1),0.3f).SetEase(Ease.OutBack);

                var canvasGroup = view.GetComponent<CanvasGroup>();
                if (canvasGroup!=null)
                {
                    canvasGroup.alpha = 0;
                    viewCanvasTweener=canvasGroup.DOFade(1, 0.3f);
                }
            }
        }


        public void ResetRect()
        {
            var rectTransform=transform.GetComponent<RectTransform>();
            rectTransform.offsetMin=Vector2.zero;
            rectTransform.offsetMax=Vector2.zero;
        }
        
        
        public override void Start()
        {
            base.Start();
            
            EventMrg.Subscribe<int>(MessageType.UiMessage,UiMessageType.Show,ShowUi);
            EventMrg.Subscribe<int>(MessageType.UiMessage,(int)UiMessageType.Hide,HideUi);
            EventMrg.Subscribe<int>(MessageType.UiMessage,UiMessageType.Remove,RemoveUi);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            EventMrg.Unsubscribe<int>(MessageType.UiMessage,UiMessageType.Show,ShowUi);
            EventMrg.Unsubscribe<int>(MessageType.UiMessage,UiMessageType.Hide,HideUi);
            EventMrg.Unsubscribe<int>(MessageType.UiMessage,UiMessageType.Remove,RemoveUi);
           
        }

        
        private void ShowUi(List<object> parma)
        {
            ShowUi((int)parma[0]);
        }

        protected virtual void ShowUi(int index)
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
        protected virtual void HideUi(int index)
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
        
        protected virtual void RemoveUi(int index)
        {
            if (index.Equals(GetIndex())||index==-1)
            {
                ABMrg.ReleaseInstantiate(GetGameObject());
                //GameObject.Destroy(GetGameObject());
            }
        }
        
        public void CloseUi()
        {
            CloseAnim((() => UiManager.RemoveUi(GetIndex())));
            
        }

        protected virtual void CloseAnim(Action end=null)
        {
            var bg=transform.Find("Bg");
            if (bg!=null)
            {
                var canvasGroup=bg.GetComponent<CanvasGroup>();
                if (canvasGroup!=null)
                {
                    bgTweener?.Kill();
                    bgTweener=canvasGroup.DOFade(0, 0.3f);  
                }
            }
            
            
            
            var view = transform.Find("View");
            if (view != null)
            {
                animTweener?.Kill();
                viewCanvasTweener?.Kill();
                animTweener=view.DOScale(new Vector3(0.4f,0.4f,0.4f),0.3f).SetEase(Ease.InOutBack);
                animTweener.onComplete += () =>
                {
                    end?.Invoke();
                };
                var canvasGroup = view.GetComponent<CanvasGroup>();
                if (canvasGroup!=null)
                {
                    viewCanvasTweener=canvasGroup.DOFade(0, 0.3f);
                }
            }
            else
            {
                end?.Invoke();
            }
        }
        
        public virtual void OnClose()
        {
        }
        
    }
}