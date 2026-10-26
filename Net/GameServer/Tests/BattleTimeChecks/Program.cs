using FrameWork.Script.Tool;
using GameData;
using Google.Protobuf;
using WebSocketDemo;

static RankData Run(long level, float seconds, int version = 1) => new()
{ Level = level, LevelTime = seconds, BattleTimeVersion = version };
static void Check(bool result, string message)
{ if (!result) throw new Exception(message); }
static void Merge(long level, float time, RankData? old, long expectedLevel, float expectedTime)
{
    var incoming = Run(level, time);
    BattleRankPolicy.MergeBest(incoming, old);
    Check(incoming.Level == expectedLevel && incoming.LevelTime == expectedTime, "Best run selection failed.");
}

Merge(2, 36, null, 2, 36);
Merge(2, 36, Run(2, 40), 2, 36);
Merge(2, 50, Run(2, 40), 2, 40);
Merge(3, 90, Run(2, 10), 3, 90);
Merge(1, 5, Run(2, 40), 2, 40);
Merge(2, 36, Run(9, 1.79e12f, 0), 2, 36);
Merge(2, 36, Run(9, 60, 0), 2, 36);
foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
{
    bool rejected = false;
    try { BattleRankPolicy.Validate(Run(1, invalid)); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected, "Invalid battle time was accepted.");
}
Check(BattleRankPolicy.IsValid(Run(1, 210)), "Valid runs above 120 seconds must be accepted.");
Check(!BattleRankPolicy.IsValid(Run(1, 36, 0)), "Legacy remaining time must not enter the battle-time leaderboard.");
var wireRun = RankData.Parser.ParseFrom(Run(1, 36.5f).ToByteArray());
Check(wireRun.LevelTime == 36.5f && wireRun.BattleTimeVersion == 1, "Battle-time protocol round trip failed.");
Check(BattleRankPolicy.CacheName("Nor") != "Nor" && BattleRankPolicy.CacheName("fightingCapacity") == "fightingCapacity", "Legacy cache isolation failed.");

float elapsed = 0, remaining = 120;
TimeTool.AdvanceBattleTime(ref elapsed, ref remaining, 36);
Check(elapsed == 36 && remaining == 84, "Elapsed seconds must differ from remaining seconds.");
TimeTool.AdvanceBattleTime(ref elapsed, ref remaining, 0);
Check(elapsed == 36, "A paused guide must not advance the clock.");
remaining = 90; // Boss phase resets its timer, but the round clock keeps running.
TimeTool.AdvanceBattleTime(ref elapsed, ref remaining, 10);
Check(elapsed == 46 && remaining == 80, "Boss countdown must follow its own configured duration.");
TimeTool.AdvanceBattleTime(ref elapsed, ref remaining, 100);
Check(elapsed == 126 && remaining == 0, "Actual elapsed time must be able to exceed 120 seconds.");
TimeTool.AdvanceBattleTime(ref elapsed, ref remaining, 1);
Check(elapsed == 126, "An expired phase kept accumulating time.");
float migratedElapsed = elapsed, migratedRemaining = remaining;
TimeTool.AdvanceBattleTime(ref migratedElapsed, ref migratedRemaining, .1f);
Check(migratedElapsed == 126, "Restoring a clock reset elapsed time.");
Check(36f.GetTimeAsFloat() == "00:36" && 120f.GetTimeAsFloat() == "02:00", "Battle time formatting failed.");
foreach (float invalid in new[] { -1f, 1.79e12f, float.NaN, float.PositiveInfinity })
    Check(invalid.GetTimeAsFloat() == "--:--", "Invalid time formatting overflowed.");
Console.WriteLine("Battle-time checks passed: actual elapsed clock, pauses, configured boss phase, runs above 120 seconds, best runs, legacy data, invalid inputs, protocol and formatting.");
