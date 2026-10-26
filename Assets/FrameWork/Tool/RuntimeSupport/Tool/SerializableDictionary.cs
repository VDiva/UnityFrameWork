using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Script.Tool
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new();

        [SerializeField]
        private List<TValue> values = new();


        // 保存时调用
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();

            foreach (var pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }


        // 加载时调用
        public void OnAfterDeserialize()
        {
            Clear();

            int count = Mathf.Min(keys.Count, values.Count);

            for (int i = 0; i < count; i++)
            {
                this[keys[i]] = values[i];
            }
        }
    }
}