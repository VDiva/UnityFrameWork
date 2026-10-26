using GameData;
using System.Text.Json;

namespace WebSocketDemo;

public static class EquipmentDeletion
{
    public const int MaxBatchSize = 100;

    // Validate the entire batch before mutating the locked player snapshot.
    public static void Apply(UserData data, IEnumerable<string> requestedIds)
    {
        var ids = requestedIds.ToArray();
        if (ids.Length == 0 || ids.Length > MaxBatchSize ||
            ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128))
            throw new ArgumentException("请选择 1 到 100 件装备，装备 ID 不能为空或超过 128 个字符。");

        var selected = ids.ToHashSet(StringComparer.Ordinal);
        var owned = data.EquipData.Select(equip => equip.Id).ToHashSet(StringComparer.Ordinal);
        if (!selected.IsSubsetOf(owned))
            throw new ArgumentException("部分装备不存在或已被删除，请刷新后重试。");

        var worn = data.DataDic.Where(pair => pair.Key.StartsWith("Equip_", StringComparison.Ordinal))
            .Select(pair => pair.Value).Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var equip in data.EquipData.Where(equip => selected.Contains(equip.Id)))
        {
            // Existing saves use the ID inside JSON for their equipment slots.
            string? legacyId = null;
            try
            {
                using var json = JsonDocument.Parse(equip.Data.ToStringUtf8());
                if (json.RootElement.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String)
                    legacyId = value.GetString();
            }
            catch (JsonException) { }
            if (worn.Contains(equip.Id) || (legacyId != null && worn.Contains(legacyId)))
                throw new InvalidOperationException("已穿戴的装备不能删除，请先卸下。");
        }

        for (int i = data.EquipData.Count - 1; i >= 0; i--)
            if (selected.Contains(data.EquipData[i].Id))
                data.EquipData.RemoveAt(i);
    }
}
