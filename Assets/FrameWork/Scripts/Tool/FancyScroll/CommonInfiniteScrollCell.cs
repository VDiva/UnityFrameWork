using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FancyScrollView
{
    /// <summary>直接挂在 Cell Prefab 根节点，不需要继承。</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CommonInfiniteScrollCell : FancyCell<int, CommonInfiniteScrollContext>
    {
        [SerializeField] private Button clickButton;

        private RectTransform rectTransform;

        
        public override void Initialize()
        {
            rectTransform = (RectTransform)transform;
            if (clickButton != null)
                clickButton.onClick.AddListener(OnClicked);
        }

        public override void UpdateContent(int index)
        {
            Context.BindItem?.Invoke(index, gameObject);
        }

        private void OnClicked()
        {
            Context.ClickItem?.Invoke(Index, gameObject);
        }

        public override void UpdatePosition(float position)
        {
            if (rectTransform == null)
                rectTransform = (RectTransform)transform;
            if (Context.Viewport == null)
                return;

            Vector2 value = rectTransform.anchoredPosition;
            if (Context.Direction == CommonScrollDirection.Vertical)
                value.y = (0.5f - position) * Context.Viewport.rect.height;
            else
                value.x = (position - 0.5f) * Context.Viewport.rect.width;
            rectTransform.anchoredPosition = value;
        }
    }
}
