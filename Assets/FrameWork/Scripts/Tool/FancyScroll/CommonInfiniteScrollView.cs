using System;
using System.Collections.Generic;
using EasingCore;
using UnityEngine;

namespace FancyScrollView
{
    /// <summary>通过下标回调绑定内容的通用无限循环列表。</summary>
    public sealed class CommonInfiniteScrollView
        : FancyScrollView<int, CommonInfiniteScrollContext>
    {
        [SerializeField] Scroller scroller;
        [SerializeField] GameObject cellPrefab;
        [SerializeField] RectTransform viewport;
        [SerializeField] CommonScrollDirection direction = CommonScrollDirection.Vertical;
        [SerializeField] bool infiniteLoop = true;
        [SerializeField, Min(0f)] float defaultScrollDuration = 0.35f;
        [SerializeField, Min(0.1f)] float dragSpeedMultiplier = 1f;
#if UNITY_EDITOR
        [Header("Editor Preview")]
        [SerializeField] bool showEditorPreview = true;
        [SerializeField, Min(1)] int editorPreviewCellCount = 6;
#endif

        private Action<int, GameObject> bindItem;
        private Action<int, GameObject> clickItem;
        private float currentScrollerPosition;

        public int Count => ItemsSource.Count;
        public int SelectedIndex { get; private set; } = -1;
        public event Action<int> SelectionChanged;
        protected override GameObject CellPrefab =>  cellPrefab;

        protected override void Initialize()
        {
            base.Initialize();
            if (scroller == null) throw new MissingReferenceException($"{name} 没有绑定 Scroller。");
            if (viewport == null) throw new MissingReferenceException($"{name} 没有绑定 Viewport。");
            loop = infiniteLoop;
            scroller.MovementType = infiniteLoop ? MovementType.Unrestricted : MovementType.Elastic;
            // Scroller 的灵敏度单位是“拖满整个 Viewport 移动多少个下标”。
            // cellInterval=0.2 表示一屏约 5 项，因此灵敏度应为 1/0.2=5。
            scroller.ScrollSensitivity = dragSpeedMultiplier / Mathf.Max(0.01f, cellInterval);
            Context.Viewport = viewport;
            Context.Direction = direction;
            Context.BindItem = (index, cell) => bindItem?.Invoke(index, cell);
            Context.ClickItem = (index, cell) => clickItem?.Invoke(index, cell);
            scroller.OnValueChanged(OnScrollerPositionChanged);
            scroller.OnSelectionChanged(OnSelectionChanged);
        }

        void OnScrollerPositionChanged(float position)
        {
            currentScrollerPosition = position;
            UpdatePosition(position);
        }

        public void SetCount(int count, Action<int, GameObject> bindAction)
            => SetCount(count, bindAction, null);

        public void SetCount(int count, Action<int, GameObject> bindAction,
            Action<int, GameObject> clickAction)
        {
            int previousSelectedIndex = SelectedIndex;
            count = Mathf.Max(0, count);
            bindItem = bindAction;
            clickItem = clickAction;
            var indices = new List<int>(count);
            for (int i = 0; i < count; i++) indices.Add(i);

            scroller.SetTotalCount(GetScrollerPositionCount(count));
            UpdateContents(indices);
            // 不调用 JumpTo(0)，保留 Scroller 当前滚动位置。
            // 第一次设置数据时默认选中 0；数量缩小时只修正为有效下标。
            SelectedIndex = count == 0
                ? -1
                : previousSelectedIndex < 0
                    ? 0
                    : NormalizeIndex(previousSelectedIndex);
        }

        public void RefreshVisible() => Refresh();

        /// <summary>
        /// 当前是否已经离开列表底部、正在查看较早的内容。
        /// 聊天列表应关闭无限循环；循环列表没有明确的底部，因此始终返回 false。
        /// </summary>
        /// <param name="threshold">距离最后一个滚动位置多少以内仍视为位于底部。</param>
        public bool IsViewingOlderItems(float threshold = 0.05f)
        {
            if (infiniteLoop || Count == 0)
                return false;

            float bottomPosition = Mathf.Max(0, GetScrollerPositionCount(Count) - 1);
            return currentScrollerPosition < bottomPosition - Mathf.Max(0, threshold);
        }

        public void ScrollTo(int index, float duration = -1f)
        {
            if (Count == 0) return;
            scroller.ScrollTo(NormalizeIndex(index),
                duration < 0f ? defaultScrollDuration : duration, Ease.OutCubic);
        }

        public void JumpTo(int index)
        {
            if (Count > 0) scroller.JumpTo(NormalizeIndex(index));
        }

        public void Next(float duration = -1f) =>
            ScrollTo(SelectedIndex < 0 ? 0 : SelectedIndex + 1, duration);

        public void Previous(float duration = -1f) =>
            ScrollTo(SelectedIndex < 0 ? 0 : SelectedIndex - 1, duration);

        void OnSelectionChanged(int index)
        {
            if (Count == 0) return;
            index = NormalizeIndex(index);
            if (SelectedIndex == index) return;
            SelectedIndex = index;
            SelectionChanged?.Invoke(index);
        }

        int NormalizeIndex(int index)
        {
            if (!infiniteLoop)
                return Mathf.Clamp(index, 0, GetScrollerPositionCount(Count) - 1);
            int result = index % Count;
            return result < 0 ? result + Count : result;
        }

        int GetScrollerPositionCount(int itemCount)
        {
            if (itemCount <= 0 || infiniteLoop)
                return itemCount;

            int visibleCount = Mathf.Max(1,
                Mathf.FloorToInt((1f - scrollOffset) / Mathf.Max(0.01f, cellInterval)) + 1);
            return Mathf.Max(1, itemCount - visibleCount + 1);
        }

#if UNITY_EDITOR
        const string PreviewObjectPrefix = "__InfinitePreview_";

        public void RefreshEditorPreview()
        {
            if (Application.isPlaying)
                return;

            ClearEditorPreview();
            if (!showEditorPreview || cellPrefab == null || cellContainer == null || viewport == null)
                return;

            int previewCount = Mathf.Max(1, editorPreviewCellCount);
            for (int i = 0; i < previewCount; i++)
            {
                CommonInfiniteScrollCell preview;
                CommonInfiniteScrollCell source = cellPrefab.GetComponent<CommonInfiniteScrollCell>();
                if (source == null)
                    return;

                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(source))
                {
                    preview = UnityEditor.PrefabUtility.InstantiatePrefab(
                        source, cellContainer) as CommonInfiniteScrollCell;
                }
                else
                {
                    preview = Instantiate(source, cellContainer);
                }
                if (preview == null)
                    continue;

                preview.name = $"{PreviewObjectPrefix}{i}";
                SetPreviewHideFlags(preview.gameObject);
                preview.enabled = false;
                preview.gameObject.SetActive(true);

                float position = scrollOffset + i * cellInterval;
                RectTransform rect = (RectTransform)preview.transform;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                Vector2 value = rect.anchoredPosition;
                if (direction == CommonScrollDirection.Vertical)
                    value.y = (0.5f - position) * viewport.rect.height;
                else
                    value.x = (position - 0.5f) * viewport.rect.width;
                rect.anchoredPosition = value;
            }
        }

        public void ClearEditorPreview()
        {
            if (Application.isPlaying || cellContainer == null)
                return;

            for (int i = cellContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = cellContainer.GetChild(i);
                if (child.name.StartsWith(PreviewObjectPrefix, StringComparison.Ordinal))
                    DestroyImmediate(child.gameObject);
            }
        }

        static void SetPreviewHideFlags(GameObject target)
        {
            target.hideFlags = HideFlags.DontSaveInEditor;
            foreach (Transform child in target.transform)
                SetPreviewHideFlags(child.gameObject);
        }
#endif
    }
}
