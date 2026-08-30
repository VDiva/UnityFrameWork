using System;
using System.Collections.Generic;
using EasingCore;
using UnityEngine;

namespace FancyScrollView
{
    /// <summary>通过下标回调绑定内容的通用虚拟化 Grid 列表。</summary>
    [RequireComponent(typeof(Scroller))]
    public sealed class CommonGridScrollView : FancyGridView<int, CommonGridScrollContext>
    {
        private sealed class CellGroup : DefaultCellGroup { }

        [SerializeField] private CommonGridScrollCell cellPrefab;
        [SerializeField] private CommonGridStartCorner startCorner = CommonGridStartCorner.UpperLeft;
        [SerializeField] private Vector2 cellSpacing = Vector2.zero;
        [SerializeField, Min(0f)] private float defaultScrollDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float defaultAlignment = 0.5f;
#if UNITY_EDITOR
        [Header("Editor Preview")]
        [SerializeField] private bool showEditorPreview = true;
        [SerializeField, Min(1)] private int editorPreviewLineCount = 3;
#endif

        private Action<int, GameObject> bindItem;
        private Action<int, GameObject> clickItem;

        public int Count => DataCount;

        protected override void SetupCellTemplate()
        {
            cellPrefab = ResolveCellPrefab();
            if (cellPrefab == null)
                throw new MissingReferenceException($"{name} 没有绑定 Grid Cell Prefab。");
            Setup<CellGroup>(cellPrefab);
        }

        private CommonGridScrollCell ResolveCellPrefab()
        {
            if (cellPrefab != null)
                return cellPrefab;
            return cellContainer == null
                ? null
                : cellContainer.GetComponentInChildren<CommonGridScrollCell>(true);
        }

        private void ApplyCellSpacing()
        {
            if (GetComponent<Scroller>().ScrollDirection == ScrollDirection.Vertical)
            {
                startAxisSpacing = Mathf.Max(0f, cellSpacing.x);
                spacing = Mathf.Max(0f, cellSpacing.y);
            }
            else
            {
                spacing = Mathf.Max(0f, cellSpacing.x);
                startAxisSpacing = Mathf.Max(0f, cellSpacing.y);
            }
        }

        protected override void Initialize()
        {
            ApplyCellSpacing();
            Context.BindItem = (index, cell) => bindItem?.Invoke(index, cell);
            Context.ClickItem = (index, cell) => clickItem?.Invoke(index, cell);
            Context.Viewport = cellContainer == null ? null : cellContainer.parent as RectTransform;
            Context.Direction = GetComponent<Scroller>().ScrollDirection;
            Context.StartCorner = startCorner;
            Context.CrossAxisCount = Mathf.Max(1, startAxisCellCount);
            Context.CrossAxisSpacing = startAxisSpacing;
            Context.CellSize = cellSize;
            base.Initialize();
        }

        public void SetCount(int count, Action<int, GameObject> bindAction)
            => SetCount(count, bindAction, null);

        public void SetCount(int count, Action<int, GameObject> bindAction,
            Action<int, GameObject> clickAction)
        {
            count = Mathf.Max(0, count);
            bindItem = bindAction;
            clickItem = clickAction;

            var indices = new List<int>(count);
            for (int i = 0; i < count; i++)
                indices.Add(i);
            UpdateContents(indices);
        }

        public void RefreshVisible()
        {
            Refresh();
        }

        public void ScrollTo(int index, float duration = -1f, float alignment = -1f,
            Action onComplete = null)
        {
            if (DataCount == 0)
                return;
            index = Mathf.Clamp(index, 0, DataCount - 1);
            float time = duration < 0f ? defaultScrollDuration : duration;
            float align = alignment < 0f ? defaultAlignment : Mathf.Clamp01(alignment);
            base.ScrollTo(index, time, Ease.OutCubic, align, onComplete);
        }

        public void JumpTo(int index, float alignment = -1f)
        {
            if (DataCount == 0)
                return;
            index = Mathf.Clamp(index, 0, DataCount - 1);
            base.JumpTo(index, alignment < 0f ? defaultAlignment : Mathf.Clamp01(alignment));
        }

#if UNITY_EDITOR
        private const string PreviewObjectPrefix = "__GridPreview_";

        /// <summary>只在编辑器生成不保存的预览 Cell。</summary>
        public void RefreshEditorPreview()
        {
            if (Application.isPlaying)
                return;

            ClearEditorPreview();
            if (!showEditorPreview || cellContainer == null)
                return;

            ApplyCellSpacing();

            cellPrefab = ResolveCellPrefab();
            if (cellPrefab == null)
                return;

            int crossCount = Mathf.Max(1, startAxisCellCount);
            int lineCount = Mathf.Max(1, editorPreviewLineCount);
            ScrollDirection scrollDirection = GetComponent<Scroller>().ScrollDirection;
            RectTransform previewViewport = cellContainer.parent as RectTransform;
            if (previewViewport == null)
                return;

            for (int line = 0; line < lineCount; line++)
            for (int cross = 0; cross < crossCount; cross++)
            {
                CommonGridScrollCell preview;
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(cellPrefab))
                {
                    preview = UnityEditor.PrefabUtility.InstantiatePrefab(
                        cellPrefab, cellContainer) as CommonGridScrollCell;
                }
                else
                {
                    preview = Instantiate(cellPrefab, cellContainer);
                }
                if (preview == null)
                    continue;

                preview.name = $"{PreviewObjectPrefix}{line}_{cross}";
                SetPreviewHideFlags(preview.gameObject);
                preview.enabled = false;
                preview.gameObject.SetActive(true);

                bool fromRight = startCorner is CommonGridStartCorner.UpperRight or
                    CommonGridStartCorner.LowerRight;
                bool fromLower = startCorner is CommonGridStartCorner.LowerLeft or
                    CommonGridStartCorner.LowerRight;
                float crossCellSize = scrollDirection == ScrollDirection.Vertical
                    ? cellSize.x : cellSize.y;
                float crossViewportSize = scrollDirection == ScrollDirection.Vertical
                    ? previewViewport.rect.width : previewViewport.rect.height;
                float crossPosition = -crossViewportSize * 0.5f + crossCellSize * 0.5f +
                                      cross * (crossCellSize + startAxisSpacing);
                if ((scrollDirection == ScrollDirection.Vertical && fromRight) ||
                    (scrollDirection == ScrollDirection.Horizontal && fromLower))
                    crossPosition = -crossPosition;
                float linePosition = paddingHead +
                                     (scrollDirection == ScrollDirection.Vertical
                                         ? cellSize.y
                                         : cellSize.x) * 0.5f +
                                     line * ((scrollDirection == ScrollDirection.Vertical
                                         ? cellSize.y
                                         : cellSize.x) + spacing);

                RectTransform rect = (RectTransform)preview.transform;
                rect.sizeDelta = cellSize;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                if (scrollDirection == ScrollDirection.Vertical)
                {
                    float y = previewViewport.rect.height * 0.5f - linePosition;
                    if (fromLower) y = -y;
                    rect.localPosition = new Vector3(crossPosition, y, 0f);
                }
                else
                {
                    float x = -previewViewport.rect.width * 0.5f + linePosition;
                    if (fromRight) x = -x;
                    rect.localPosition = new Vector3(x, crossPosition, 0f);
                }
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

        private static void SetPreviewHideFlags(GameObject target)
        {
            target.hideFlags = HideFlags.DontSaveInEditor;
            foreach (Transform child in target.transform)
                SetPreviewHideFlags(child.gameObject);
        }
#endif
    }
}
