using System;
using System.Collections.Generic;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FrameWork
{
    public class UiActor: Actor
    {
        protected Tweener animTweener;
        protected Tweener viewCanvasTweener;
        protected Tweener bgTweener;
        private bool _animRefsCached;
        private Transform _bgTransform;
        private CanvasGroup _bgCanvasGroup;
        private Transform _viewTransform;
        private CanvasGroup _viewCanvasGroup;
        private RectTransform _rectTransform;

        protected UiActor(): base() {}
        protected UiActor(Transform trans): base(trans) {}

        private void EnsureAnimRefs()
        {
            if (_animRefsCached || GetGameObject() == null) return;
            _animRefsCached = true;
            _bgTransform = transform.Find("Bg");
            if (_bgTransform != null) _bgCanvasGroup = _bgTransform.GetComponent<CanvasGroup>();
            _viewTransform = transform.Find("View");
            if (_viewTransform != null) _viewCanvasGroup = _viewTransform.GetComponent<CanvasGroup>();
        }


        public virtual void Open(object[] objects)
        {
            //Time.timeScale = 0;
            //OpenAnim();
        }

        public virtual void OpenAnim()
        {
            EnsureAnimRefs();
            if (_bgCanvasGroup != null)
            {
                bgTweener?.Kill();
                _bgCanvasGroup.alpha = 0;
                bgTweener = _bgCanvasGroup.DOFade(1, 0.3f);
            }

            if (_viewTransform != null)
            {
                animTweener?.Kill();
                viewCanvasTweener?.Kill();
                _viewTransform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                animTweener = _viewTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

                if (_viewCanvasGroup != null)
                {
                    _viewCanvasGroup.alpha = 0;
                    viewCanvasTweener = _viewCanvasGroup.DOFade(1, 0.3f);
                }
            }
        }


        public void ResetRect()
        {
            if (_rectTransform == null) _rectTransform = transform.GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;
            }
        }
        
        
        public override void Start()
        {
            base.Start();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            animTweener?.Kill();
            viewCanvasTweener?.Kill();
            bgTweener?.Kill();
            UiManager.ForgetUi(this);
        }

        protected virtual void ShowUi(int index)
        {
            if (index.Equals(GetIndex()))
            {
                if (ReferenceEquals(UiManager.GetUiByType(GetType()), this))
                    UiManager.OpenUi(GetType()).Forget(Debug.LogException);
                else if (GetGameObject() != null) GetGameObject().SetActive(true);
            }
        }

        protected virtual void HideUi(int index)
        {
            if (index.Equals(GetIndex())|| index==-1)
            {
                if (ReferenceEquals(UiManager.GetUiByType(GetType()), this))
                    UiManager.HideUi(GetType());
                else if (GetGameObject() != null) GetGameObject().SetActive(false);
            }
        }
        
        protected virtual void RemoveUi(int index)
        {
            if (index.Equals(GetIndex())||index==-1)
            {
                if (ReferenceEquals(UiManager.GetUiByType(GetType()), this))
                    UiManager.RemoveUi(GetType());
                else ABMrg.ReleaseInstantiate(GetGameObject());
            }
        }
        
        public void CloseUi()
        {
            CloseAnim(() => UiManager.HideUi(GetIndex()));
        }

        protected virtual void CloseAnim(Action end=null)
        {
            EnsureAnimRefs();
            if (_bgCanvasGroup != null)
            {
                bgTweener?.Kill();
                bgTweener = _bgCanvasGroup.DOFade(0, 0.3f);
            }

            if (_viewTransform != null)
            {
                animTweener?.Kill();
                viewCanvasTweener?.Kill();
                animTweener = _viewTransform.DOScale(new Vector3(0.4f, 0.4f, 0.4f), 0.3f).SetEase(Ease.InOutBack);
                if (end != null)
                {
                    animTweener.onComplete += () => end();
                }
                if (_viewCanvasGroup != null)
                {
                    viewCanvasTweener = _viewCanvasGroup.DOFade(0, 0.3f);
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