using System;
using UnityEngine;
using UnityEngine.UI;

namespace FancyScrollView
{
    /// <summary>直接挂在 Grid Cell Prefab 根节点。</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CommonGridScrollCell : FancyGridViewCell<int, CommonGridScrollContext>
    {
        [SerializeField] private Button clickButton;

        public event Action<int, GameObject> ContentUpdated;
        public event Action<int, GameObject> Clicked;

        public override void Initialize()
        {
            if (clickButton != null)
                clickButton.onClick.AddListener(OnClicked);
        }

        public override void UpdateContent(int index)
        {
            Context.BindItem?.Invoke(index, gameObject);
            ContentUpdated?.Invoke(index, gameObject);
        }

        private void OnClicked()
        {
            Context.ClickItem?.Invoke(Index, gameObject);
            Clicked?.Invoke(Index, gameObject);
        }

        protected override void UpdatePosition(float normalizedPosition, float localPosition)
        {
            base.UpdatePosition(normalizedPosition, localPosition);
            if (Context.Viewport == null)
                return;

            bool fromRight = Context.StartCorner is CommonGridStartCorner.UpperRight or
                CommonGridStartCorner.LowerRight;
            bool fromLower = Context.StartCorner is CommonGridStartCorner.LowerLeft or
                CommonGridStartCorner.LowerRight;
            Vector3 position = transform.localPosition;

            if (Context.Direction == ScrollDirection.Vertical)
            {
                if (fromRight) position.x = -position.x;
                float groupWidth = Context.CrossAxisCount * Context.CellSize.x +
                                   (Context.CrossAxisCount - 1) * Context.CrossAxisSpacing;
                float offset = Mathf.Max(0f, Context.Viewport.rect.width - groupWidth) * 0.5f;
                position.x += fromRight ? offset : -offset;
                if (fromLower) position.y = -position.y;
            }
            else
            {
                if (fromLower) position.y = -position.y;
                float groupHeight = Context.CrossAxisCount * Context.CellSize.y +
                                    (Context.CrossAxisCount - 1) * Context.CrossAxisSpacing;
                float offset = Mathf.Max(0f, Context.Viewport.rect.height - groupHeight) * 0.5f;
                position.y += fromLower ? -offset : offset;
                if (fromRight) position.x = -position.x;
            }

            transform.localPosition = position;
        }
    }
}
