using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FancyScrollView
{
    [Serializable]
    public sealed class CommonInfiniteScrollPositionEvent : UnityEvent<float> { }

    /// <summary>直接挂在 Cell Prefab 根节点，不需要继承。</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CommonInfiniteScrollCell : FancyCell<int, CommonInfiniteScrollContext>
    {
        [SerializeField] private Button clickButton;

        private RectTransform rectTransform;

        public CommonInfiniteScrollPositionEvent positionUpdate =
            new CommonInfiniteScrollPositionEvent();

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
            positionUpdate?.Invoke(position);
            PositionCell(rectTransform, Context.Viewport, Context.Direction, position,
                Context.AlignToViewport);
        }

        internal static void PositionCell(RectTransform cell, RectTransform viewport,
            CommonScrollDirection direction, float position, bool alignToViewport)
        {
            Vector2 value = cell.anchoredPosition;
            if (direction == CommonScrollDirection.Vertical)
            {
                float anchor = alignToViewport
                    ? Mathf.Lerp(cell.anchorMin.y, cell.anchorMax.y, cell.pivot.y)
                    : 0.5f;
                value.y = (1f - position - anchor) * viewport.rect.height;
            }
            else
            {
                float anchor = alignToViewport
                    ? Mathf.Lerp(cell.anchorMin.x, cell.anchorMax.x, cell.pivot.x)
                    : 0.5f;
                value.x = (position - anchor) * viewport.rect.width;
            }
            cell.anchoredPosition = value;
        }
    }
}
