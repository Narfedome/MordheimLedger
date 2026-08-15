namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Henchmen don't roll on the Heroes' D66 Serious Injury table (see SeriousInjuryTable) - a single D6
/// decides whether they survive being taken Out of Action: 1-2 dead/retired, 3-6 full recovery, no
/// sub-table at all. Verified against the rulebook (p. 118) via RulesReference/Campagne.md.
/// </summary>
public static class HenchmanInjuryTable
{
    private const int DeathThreshold = 2;

    public static bool IsDeath(int roll) => roll is >= 1 and <= DeathThreshold;

    public static bool TryGetTextKey(int roll, out string key)
    {
        if (roll is < 1 or > 6)
        {
            key = string.Empty;
            return false;
        }

        key = IsDeath(roll) ? "HenchmanInjuryDead" : "HenchmanInjuryRecovered";
        return true;
    }

    /// <summary>Rolls one D6.</summary>
    public static int RollDice() => Random.Shared.Next(1, 7);
}
