using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsInventory : MonoBehaviour
    {
        private readonly Dictionary<FpsItemDefinition, int> counts = new Dictionary<FpsItemDefinition, int>();
        private bool usingItem;

        public event Action<FpsItemDefinition, int> Changed;

        public int GetCount(FpsItemDefinition item) =>
            item != null && counts.TryGetValue(item, out int count) ? count : 0;

        public int Add(FpsItemDefinition item, int amount)
        {
            if (usingItem || item == null || amount <= 0) return 0;

            int current = GetCount(item);
            int added = Mathf.Min(amount, item.CarryLimit - current);
            if (added <= 0) return 0;

            counts[item] = current + added;
            Changed?.Invoke(item, counts[item]);
            return added;
        }

        public bool TryUse(FpsItemDefinition item)
        {
            if (usingItem || GetCount(item) == 0) return false;

            usingItem = true;
            try
            {
                if (!item.TryApply(gameObject)) return false;
                int remaining = counts[item] - 1;
                if (remaining == 0) counts.Remove(item);
                else counts[item] = remaining;
            }
            finally
            {
                usingItem = false;
            }

            Changed?.Invoke(item, GetCount(item));
            return true;
        }
    }
}
