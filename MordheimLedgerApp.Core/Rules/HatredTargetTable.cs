namespace MordheimLedgerApp.Core.Rules;

/// <summary>What kind of target a "Rancune"/Bitter Enmity 1D6 sub-roll produced - see
/// HatredTargetTable.TryGetOutcome. Drives which picker the End of Game wizard shows and which field
/// on WarriorHatred gets set.</summary>
public enum HatredTargetKind
{
    /// <summary>A specific enemy Warrior (1-4). The rulebook splits this into "the individual
    /// responsible" (1-3, redirected to the enemy leader if that individual was a Henchman - Henchmen
    /// are never tracked as named individuals in this app, so the picker is scoped to Heroes only,
    /// which already covers that redirect) and "the leader" (4) - both resolve to the same picker
    /// (a Hero of the chosen enemy warband), so a single kind covers both.</summary>
    SpecificWarrior,

    /// <summary>The enemy's entire specific Warband (5).</summary>
    SpecificWarband,

    /// <summary>Every Warband of that WarbandArchetype (6).</summary>
    WarbandArchetype
}

/// <summary>Reference lookup for the "Rancune"/Bitter Enmity Serious Injury result's 1D6 sub-roll (see
/// SeriousInjuryTable.IsBitterEnmity) - not itself part of SeriousInjuryTable since it's a single
/// result's sub-table, not a full D66/D6 table of its own. Verified against the rulebook text
/// (RulesReference/Campagne.md):
/// 1-3 the individual responsible (or the enemy leader if a Henchman), 4 that warband's leader,
/// 5 that entire warband, 6 all warbands of that type.</summary>
public static class HatredTargetTable
{
    public static bool TryGetOutcome(int d6Roll, out HatredTargetKind kind)
    {
        switch (d6Roll)
        {
            case 1 or 2 or 3 or 4:
                kind = HatredTargetKind.SpecificWarrior;
                return true;
            case 5:
                kind = HatredTargetKind.SpecificWarband;
                return true;
            case 6:
                kind = HatredTargetKind.WarbandArchetype;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>Rolls 1D6.</summary>
    public static int RollDice() => Random.Shared.Next(1, 7);
}
