namespace MordheimLedgerApp.Core.Rules;

/// <summary>"Exploration procedure" (p.134) - a SECOND, universal effect of every Exploration roll,
/// entirely separate from whatever specific location result a double/triple/etc. may also trigger (see
/// ExplorationChart.DetectMultiples/ExplorationOutcomeResolver): "Add the results together and consult
/// the chart below to see how many shards of wyrdstone you have found." Always applies, additively, to
/// EVERY Exploration roll a warband makes (kept dice only, per ExplorationChart.ComputeDiceCount) -
/// distinct from any wyrdstone a specific chart entry (e.g. The Well/Ripped Building/The Pit) grants on
/// top of this baseline.</summary>
public static class WyrdstoneShardsTable
{
    /// <summary>0 for a non-positive total (no dice kept, e.g. no surviving Heroes this game).</summary>
    public static int GetShards(int diceTotal) => diceTotal switch
    {
        <= 0 => 0,
        <= 5 => 1,
        <= 11 => 2,
        <= 17 => 3,
        <= 24 => 4,
        <= 30 => 5,
        <= 35 => 6,
        _ => 7
    };
}
