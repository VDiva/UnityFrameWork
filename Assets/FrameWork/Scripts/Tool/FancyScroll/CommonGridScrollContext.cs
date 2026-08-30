using System;
using UnityEngine;

namespace FancyScrollView
{
    public enum CommonGridStartCorner
    {
        UpperLeft,
        UpperRight,
        LowerLeft,
        LowerRight
    }

    public sealed class CommonGridScrollContext : FancyGridViewContext
    {
        public Action<int, GameObject> BindItem;
        public Action<int, GameObject> ClickItem;
        public RectTransform Viewport;
        public ScrollDirection Direction;
        public CommonGridStartCorner StartCorner;
        public int CrossAxisCount;
        public float CrossAxisSpacing;
        public Vector2 CellSize;
    }
}
