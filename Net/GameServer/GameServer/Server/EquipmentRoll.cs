namespace WebSocketDemo;

/// <summary>所有成员先掷一次，最高点相同的成员继续重掷，直到产生唯一获胜者。</summary>
public static class EquipmentRoll
{
    public sealed record RollScore(int PlayerIndex, int Score);
    public sealed record Result(int WinnerIndex, IReadOnlyList<RollScore[]> Rounds);

    public static Result Resolve(int playerCount, Func<int> roll)
    {
        if (playerCount <= 0) throw new ArgumentOutOfRangeException(nameof(playerCount));
        var candidates = Enumerable.Range(0, playerCount).ToArray();
        var rounds = new List<RollScore[]>();
        do
        {
            var scores = candidates.Select(index => new RollScore(index, roll())).ToArray();
            if (scores.Any(s => s.Score < 1 || s.Score > 100))
                throw new InvalidOperationException("骰子点数必须在 1 到 100 之间。");
            rounds.Add(scores);
            int max = scores.Max(s => s.Score);
            candidates = scores.Where(s => s.Score == max).Select(s => s.PlayerIndex).ToArray();
        } while (candidates.Length > 1);
        return new Result(candidates[0], rounds);
    }
}
