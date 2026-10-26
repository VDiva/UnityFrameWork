using System;
using System.Collections.Generic;
using EasingCore;
using UnityEngine;
using UnityEngine.Events;

namespace FancyScrollView
{
    [Serializable]
    public sealed class CommonInfiniteScrollSelectionEvent : UnityEvent<int> { }

    public enum CommonScrollContentAlignment
    {
        [InspectorName("沿用 Scroll Offset")]
        UseScrollOffset,
        [InspectorName("左 / 上")]
        Start,
        [InspectorName("居中")]
        Center,
        [InspectorName("右 / 下")]
        End
    }

    /// <summary>通过下标回调绑定内容的通用无限循环列表。</summary>
    public sealed class CommonInfiniteScrollView
        : FancyScrollView<int, CommonInfiniteScrollContext>
    {
        [SerializeField] Scroller scroller;
        [SerializeField] GameObject cellPrefab;
        [SerializeField] RectTransform viewport;
        [SerializeField] CommonScrollDirection direction = CommonScrollDirection.Vertical;
        [SerializeField, Tooltip("内容能完整放进 Viewport 时整组对齐；内容较多或循环滚动时，滚动/吸附的当前项对齐。横向为左/中/右，竖向为上/中/下。")]
        CommonScrollContentAlignment contentAlignment = CommonScrollContentAlignment.UseScrollOffset;
        [SerializeField] bool infiniteLoop = true;
        [SerializeField, Min(0f)] float defaultScrollDuration = 0.35f;
        [SerializeField, Min(0.1f)] float dragSpeedMultiplier = 1f;
        [SerializeField, InspectorName("SelectionChanged")]
        CommonInfiniteScrollSelectionEvent onSelectionChanged = new CommonInfiniteScrollSelectionEvent();
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
        protected override float CellVisibilityMarginStart
        {
            get
            {
                if (!IsContentAlignmentActive(Count) || IsContentFullyVisible(Count) ||
                    !TryGetCellExtent(out float leadingExtent, out float extent))
                    return 0f;
                return Mathf.Max(0f, extent - leadingExtent);
            }
        }

        protected override float CellVisibilityMarginEnd
        {
            get
            {
                if (!IsContentAlignmentActive(Count) || IsContentFullyVisible(Count) ||
                    !TryGetCellExtent(out float leadingExtent, out _))
                    return 0f;
                return Mathf.Max(0f, leadingExtent);
            }
        }

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
            Context.AlignToViewport = IsContentAlignmentActive(ItemsSource.Count);
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

        protected override void UpdatePosition(float position)
        {
            float originalOffset = scrollOffset;
            Context.AlignToViewport = IsContentAlignmentActive(ItemsSource.Count);
            scrollOffset = GetEffectiveScrollOffset(ItemsSource.Count);
            try
            {
                base.UpdatePosition(position);
            }
            finally
            {
                scrollOffset = originalOffset;
            }
        }

        protected override void Refresh()
        {
            float originalOffset = scrollOffset;
            Context.AlignToViewport = IsContentAlignmentActive(ItemsSource.Count);
            scrollOffset = GetEffectiveScrollOffset(ItemsSource.Count);
            try
            {
                base.Refresh();
            }
            finally
            {
                scrollOffset = originalOffset;
            }
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

            bool resetScrollPosition = IsContentFullyVisible(count) &&
                                       !Mathf.Approximately(scroller.Position, 0f);
            scroller.SetTotalCount(GetScrollerPositionCount(count));
            // 先按归零位置刷新新数据，再同步 Scroller，避免旧位置触发多余的绑定回调。
            if (resetScrollPosition)
                currentPosition = 0f;
            UpdateContents(indices);
            if (resetScrollPosition)
                scroller.Position = 0f;
            // 普通滚动保留当前位置；完整内容对齐时没有滚动范围，位置归零。
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
            onSelectionChanged?.Invoke(index);
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

            if (IsContentAlignmentActive(itemCount))
                return IsContentFullyVisible(itemCount) ? 1 : itemCount;

            int visibleCount = Mathf.Max(1,
                Mathf.FloorToInt((1f - scrollOffset) / Mathf.Max(0.01f, cellInterval)) + 1);
            return Mathf.Max(1, itemCount - visibleCount + 1);
        }

        bool IsContentAlignmentActive(int itemCount)
        {
            return contentAlignment != CommonScrollContentAlignment.UseScrollOffset &&
                   itemCount > 0 && TryGetCellExtent(out _, out _);
        }

        bool IsContentFullyVisible(int itemCount)
        {
            return !infiniteLoop && IsContentAlignmentActive(itemCount) &&
                   TryGetCellExtent(out _, out float extent) &&
                   extent + (itemCount - 1) * cellInterval <= 1f;
        }

        float GetEffectiveScrollOffset(int itemCount)
        {
            if (!IsContentAlignmentActive(itemCount) ||
                !TryGetCellExtent(out float leadingExtent, out float extent))
                return scrollOffset;

            int alignedCount = IsContentFullyVisible(itemCount) ? itemCount : 1;
            // Cell 大于 Viewport 时保留负的剩余空间，才能按 Cell 的实际尺寸居中或靠尾对齐。
            float freeSpace = 1f - extent - (alignedCount - 1) * cellInterval;
            float alignment = contentAlignment == CommonScrollContentAlignment.Center ? 0.5f :
                contentAlignment == CommonScrollContentAlignment.End ? 1f : 0f;
            float offset = leadingExtent + freeSpace * alignment;
            // FancyScrollView 的对象池不创建 position 恰好为 1 的 Cell。
            if (Mathf.Approximately(offset + (alignedCount - 1) * cellInterval, 1f))
                offset -= 0.0001f;
            return offset;
        }

        bool TryGetCellExtent(out float leadingExtent, out float extent)
        {
            leadingExtent = 0f;
            extent = 0f;
            if (cellPrefab == null || viewport == null)
                return false;

            RectTransform cell = cellPrefab.transform as RectTransform;
            if (cell == null)
                return false;

            bool vertical = direction == CommonScrollDirection.Vertical;
            float viewportSize = vertical ? viewport.rect.height : viewport.rect.width;
            float cellSize = vertical ? cell.rect.height : cell.rect.width;
            if (viewportSize <= 0f || cellSize <= 0f)
                return false;

            extent = cellSize / viewportSize;
            leadingExtent = vertical ? (1f - cell.pivot.y) * extent : cell.pivot.x * extent;
            return true;
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
            bool alignToViewport = IsContentAlignmentActive(previewCount);
            float previewOffset = GetEffectiveScrollOffset(previewCount);
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

                float position = previewOffset + i * cellInterval;
                RectTransform rect = (RectTransform)preview.transform;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                CommonInfiniteScrollCell.PositionCell(rect, viewport, direction, position,
                    alignToViewport);
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
