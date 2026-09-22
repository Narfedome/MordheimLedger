namespace MordheimLedgerApp.Core.Rules;

/// <summary>"Selling Wyrdstone" (post-battle sequence step 4, right after Determine Exploration Results) -
/// a pure lookup table (p.134), not a formula: net gold crowns for selling a batch of shards in one
/// transaction, already accounting for the warband's upkeep (a bigger warband nets less per shard sold -
/// more mouths to feed). Rows = shards sold at once (1 to "8+", clamped), columns = warriors currently in
/// the warband, bucketed (1-3/4-6/7-9/10-12/13-15/16+). Selling is never obligatory, and the shard count
/// consulted here is whatever the player chooses to sell THIS time, not necessarily the warband's whole
/// stock (Warband.WyrdstoneShards) - see EndOfGamePageViewModel's Wyrdstone Sale step.</summary>
public static class WyrdstoneSaleTable
{
    // [shardsSold - 1, warriorCountBucket] -> net gold crowns. Source: book table, p.134.
    private static readonly int[,] NetGoldByShardsAndWarriorBucket =
    {
        { 45, 40, 35, 30, 30, 25 },
        { 60, 55, 50, 45, 40, 35 },
        { 75, 70, 65, 60, 55, 50 },
        { 90, 80, 70, 65, 60, 55 },
        { 110, 100, 90, 80, 70, 65 },
        { 120, 110, 100, 90, 80, 70 },
        { 145, 130, 120, 110, 100, 90 },
        { 155, 140, 130, 120, 110, 100 },
    };

    /// <summary>0 for shardsSold &lt;= 0 (nothing to sell). Both dimensions clamp to the table's last row/
    /// column ("8+" shards, "16+" warriors) rather than throwing for a warband/batch bigger than the
    /// table covers.</summary>
    public static int GetNetGold(int shardsSold, int warriorCount)
    {
        if (shardsSold <= 0) return 0;

        var row = Math.Min(shardsSold, 8) - 1;
        var column = GetWarriorCountBucketIndex(warriorCount) - 1;
        return NetGoldByShardsAndWarriorBucket[row, column];
    }

    /// <summary>1-6, matching the table's 6 warrior-count columns in order (1-3/4-6/7-9/10-12/13-15/16+) -
    /// exposed separately (not just an internal GetNetGold detail) so the wizard's reference table
    /// display (EndOfGamePageViewModel.WyrdstoneSaleTableRows) can highlight the single cell that
    /// applies to the current warband, without duplicating these bucket boundaries in the UI layer.</summary>
    public static int GetWarriorCountBucketIndex(int warriorCount) => warriorCount switch
    {
        <= 3 => 1,
        <= 6 => 2,
        <= 9 => 3,
        <= 12 => 4,
        <= 15 => 5,
        _ => 6
    };
}
