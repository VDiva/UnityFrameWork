using GameData;

namespace WebSocketDemo;

public static class BattleRankPolicy
{
    public const string RankName = "Nor";
    public const int Version = 1;

    public static bool IsValid(RankData data) => data.BattleTimeVersion == Version &&
        float.IsFinite(data.LevelTime) && data.LevelTime > 0;

    public static void Validate(RankData data)
    {
        if (!IsValid(data) || data.Level < 0)
            throw new ArgumentException("战斗成绩必须使用新版实际耗时，且为有效的正秒数。");
    }

    public static void MergeBest(RankData incoming, RankData? existing)
    {
        Validate(incoming);
        if (existing != null && IsValid(existing) &&
            (existing.Level > incoming.Level ||
             (existing.Level == incoming.Level && existing.LevelTime <= incoming.LevelTime)))
        {
            incoming.Level = existing.Level;
            incoming.LevelTime = existing.LevelTime;
        }
    }

    public static string CacheName(string rankName) => rankName == RankName
        ? $"{rankName}:battle-time-v{Version}" : rankName;
}
