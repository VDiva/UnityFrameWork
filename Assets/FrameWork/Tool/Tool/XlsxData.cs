using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace FrameWork
{
    internal static class XlsxFieldCache
    {
        private static readonly ConcurrentDictionary<(Type, string), FieldInfo> Fields =
            new ConcurrentDictionary<(Type, string), FieldInfo>();

        internal static TKey Read<TKey, TRow>(object row, string name, int index)
        {
            if (!(row is TRow)) throw new ArgumentException($"配置表第 {index + 1} 条数据为空或类型不匹配：{typeof(TRow).Name}");
            var type = row.GetType();
            var field = Fields.GetOrAdd((type, name), key => key.Item1.GetField(key.Item2, BindingFlags.Public | BindingFlags.Instance) ??
                throw new ArgumentException($"配置表 {key.Item1.Name} 缺少公开字段 {key.Item2}"));
            if (!typeof(TKey).IsAssignableFrom(field.FieldType))
                throw new ArgumentException($"配置表 {type.Name}.{name} 类型 {field.FieldType.Name} 与索引类型 {typeof(TKey).Name} 不匹配。");
            var value = field.GetValue(row);
            if (value == null) throw new ArgumentException($"配置表第 {index + 1} 条数据的索引 {name} 为空。");
            return (TKey)value;
        }

        internal static void Validate(string key, IList rows)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("索引字段不能为空。", nameof(key));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
        }
    }

    public class XlsxData<T, TV>
    {
        public Dictionary<T, List<TV>> _dicData;

        /// <summary>零反射、零装箱的强类型索引构造（推荐，由配置表生成代码直接调用）。</summary>
        public XlsxData(Func<TV, T> keySelector, IList<TV> rows)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            _dicData = new Dictionary<T, List<TV>>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) throw new ArgumentException($"配置表第 {i + 1} 条数据为空：{typeof(TV).Name}");
                var index = keySelector(row);
                if (index == null) throw new ArgumentException($"配置表第 {i + 1} 条数据的索引为空：{typeof(TV).Name}");
                if (!_dicData.TryGetValue(index, out var group))
                {
                    group = new List<TV>(1);
                    _dicData.Add(index, group);
                }
                group.Add(row);
            }
        }

        /// <summary>兼容旧版基于字段名反射的索引构造。</summary>
        public XlsxData(string key, IList obj)
        {
            XlsxFieldCache.Validate(key, obj);
            _dicData = new Dictionary<T, List<TV>>(obj.Count);
            for (int i = 0; i < obj.Count; i++)
            {
                var index = XlsxFieldCache.Read<T, TV>(obj[i], key, i);
                if (!_dicData.TryGetValue(index, out var group))
                {
                    group = new List<TV>(1);
                    _dicData.Add(index, group);
                }
                group.Add((TV)obj[i]);
            }
        }

        public TV ByKeyGetValue(T key) => _dicData.TryGetValue(key, out var data) && data.Count > 0 ? data[0] : default;
        public List<TV> ByKeyGetValues(T key) => _dicData.TryGetValue(key, out var data) ? data : default;
    }

    public class XlsxData<T, TK, TV>
    {
        public Dictionary<T, Dictionary<TK, TV>> _dicData;

        /// <summary>零反射、零装箱的强类型联合索引构造。</summary>
        public XlsxData(Func<TV, T> key1Selector, Func<TV, TK> key2Selector, IList<TV> rows)
        {
            if (key1Selector == null) throw new ArgumentNullException(nameof(key1Selector));
            if (key2Selector == null) throw new ArgumentNullException(nameof(key2Selector));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            _dicData = new Dictionary<T, Dictionary<TK, TV>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) throw new ArgumentException($"配置表第 {i + 1} 条数据为空：{typeof(TV).Name}");
                var index = key1Selector(row);
                var index2 = key2Selector(row);
                if (index == null || index2 == null)
                    throw new ArgumentException($"配置表第 {i + 1} 条数据的联合索引为空：{typeof(TV).Name}");
                if (!_dicData.TryGetValue(index, out var group))
                {
                    group = new Dictionary<TK, TV>();
                    _dicData.Add(index, group);
                }
                if (group.ContainsKey(index2))
                    throw new ArgumentException($"配置表第 {i + 1} 条数据出现重复联合索引：{index}, {index2}");
                group.Add(index2, row);
            }
        }

        /// <summary>兼容旧版基于字段名反射的联合索引构造。</summary>
        public XlsxData(string key, string key2, IList obj)
        {
            XlsxFieldCache.Validate(key, obj);
            XlsxFieldCache.Validate(key2, obj);
            _dicData = new Dictionary<T, Dictionary<TK, TV>>();
            for (int i = 0; i < obj.Count; i++)
            {
                var index = XlsxFieldCache.Read<T, TV>(obj[i], key, i);
                var index2 = XlsxFieldCache.Read<TK, TV>(obj[i], key2, i);
                if (!_dicData.TryGetValue(index, out var group))
                {
                    group = new Dictionary<TK, TV>();
                    _dicData.Add(index, group);
                }
                if (group.ContainsKey(index2))
                    throw new ArgumentException($"配置表第 {i + 1} 条数据出现重复联合索引：{key}={index}, {key2}={index2}");
                group.Add(index2, (TV)obj[i]);
            }
        }

        public TV ByKeyGetValue(T key, TK key2) =>
            _dicData.TryGetValue(key, out var group) && group.TryGetValue(key2, out var value) ? value : default;
    }
}
