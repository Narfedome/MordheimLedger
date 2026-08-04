namespace MordheimLedgerApp.Services;

/// <summary>
/// Henchmen don't roll on the Heroes' D66 Serious Injury table (see SeriousInjuryTable) - a single D6
/// decides whether they survive being taken Out of Action: 1-2 dead/retired, 3-6 full recovery, no
/// sub-table at all. Verified against the rulebook (p. 118) via RulesReference/Campagne.md.
/// </summary>
public static class HenchmanInjuryTable
{
    private const int DeathThreshold = 2;

    public static bool IsDeath(int roll) => roll is >= 1 and <= DeathThreshold;

    public static bool TryGet(int roll, out string text)
    {
        if (roll is < 1 or > 6)
        {
            text = string.Empty;
            return false;
        }

        text = LocalizationService.Instance[IsDeath(roll) ? "HenchmanInjuryDead" : "HenchmanInjuryRecovered"];
        return true;
    }

    /// <summary>Rolls one D6 and looks up the result.</summary>
    public static (int Roll, string Text) Roll()
    {
        var roll = Random.Shared.Next(1, 7);
        TryGet(roll, out var text);
        return (roll, text);
    }
}
