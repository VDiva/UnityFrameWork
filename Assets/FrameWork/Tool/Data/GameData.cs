using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Data
{
    public static class GameData
    {
        private static readonly Dictionary<string, string> _dataDic = new Dictionary<string, string>();
        private static bool _dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _dataDic.Clear();
            _dirty = false;
        }

        public static bool HasKey(string key) => _dataDic.ContainsKey(key) || PlayerPrefs.HasKey(key);

        public static void SetString(string key, string value)
        {
            _dataDic[key] = value;
            PlayerPrefs.SetString(key, value);
            _dirty = true;
        }

        public static void AddValue(string key, string value)
        {
            SetString(key, value);
        }

        public static string GetValue(string key, string defaultValue)
        {
            if (!_dataDic.TryGetValue(key, out var cached))
            {
                cached = PlayerPrefs.GetString(key, defaultValue);
                _dataDic[key] = cached;
            }
            return cached;
        }

        public static void DeleteKey(string key)
        {
            _dataDic.Remove(key);
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                _dirty = true;
            }
        }

        /// <summary>有未落盘改动时显式写入磁盘（适合批量写入后或切后台时调用）。</summary>
        public static void Flush()
        {
            if (!_dirty) return;
            PlayerPrefs.Save();
            _dirty = false;
        }

        public static int GetProperty(string propertyName, string type = "Nor", int defaultValue = 0)
        {
            var v = GetValue($"property_{type}_" + propertyName, defaultValue.ToString());
            return v.ToInt();
        }

        public static void AddProperty(string propertyName, int count, string type = "Nor")
        {
            var key = $"property_{type}_" + propertyName;
            var v = GetProperty(propertyName, type) + count;
            v = Mathf.Max(0, v);
            SetString(key, v.ToString());
        }

        public static void SetProperty(string propertyName, int count, string type = "Nor")
        {
            var key = $"property_{type}_" + propertyName;
            SetString(key, count.ToString());
        }
    }
}
