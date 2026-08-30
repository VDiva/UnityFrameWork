using System;
using UnityEngine;

namespace FancyScrollView
{
    public enum CommonScrollDirection
    {
        Vertical,
        Horizontal
    }

    public sealed class CommonInfiniteScrollContext
    {
        public RectTransform Viewport;
        public CommonScrollDirection Direction;
        public Action<int, GameObject> BindItem;
        public Action<int, GameObject> ClickItem;
    }
}
