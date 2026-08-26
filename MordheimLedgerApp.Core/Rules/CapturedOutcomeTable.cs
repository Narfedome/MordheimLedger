namespace MordheimLedgerApp.Core.Rules;

/// <summary>The 5 named outcomes for "Captured" (Serious Injury roll 61) - all of them play out from
/// the CAPTOR's perspective (their choice, their gain: gold/Zombie/Experience all go to the opposing
/// warband, never this one) - the app has no concept of an opponent's warband as structured data (same
/// stance as Hatred's free-text targets), so every option is always offered rather than gated on the
/// opponent's actual race. What matters for THIS warband's own ledger is only whether the warrior comes
/// back or is gone for good - see ReturnsToWarband/CausesDeath.</summary>
public enum CapturedOutcome
{
    /// <summary>Ransomed at a price set by the captor - no fixed formula in the rulebook (a real
    /// negotiation between two players), so no Treasury effect is enforced here; the player can adjust
    /// Treasury by hand afterward if they agreed on a price. Warrior returns with all equipment.</summary>
    Ransomed,

    /// <summary>Exchanged for one of the captor's own warband held captive by this one - same "returns
    /// with equipment" outcome as Ransomed, distinguished only for the History sentence's flavor.</summary>
    Exchanged,

    /// <summary>Sold to slavers for 1D6x5 gc - paid to the CAPTOR, not this warband, so no Treasury
    /// gain here. The warrior is gone for good but not confirmed dead (unlike the two branches below),
    /// so this maps to WarriorStatus.Retired rather than Dead - same "permanently out, never explicitly
    /// killed" idiom as the second Blinded-in-One-Eye case.</summary>
    SoldToSlavers,

    /// <summary>Only a real branch if the captor is Undead - offered unconditionally regardless (see
    /// class doc). The warrior is killed and becomes a Zombie for the CAPTOR's warband, not this one -
    /// from this warband's perspective, purely a death.</summary>
    KilledByUndead,

    /// <summary>Only a real branch if the captor is Possessed - the captor's leader gains +1 Experience
    /// (again, the CAPTOR's leader, not this warband's). Purely a death from this warband's side.</summary>
    SacrificedByPossessed
}

public static class CapturedOutcomeTable
{
    /// <summary>True for Ransomed/Exchanged - the warrior stays with the warband and keeps all
    /// equipment, no further mutation needed. False for every other outcome (equipment lost either
    /// way, see WarbandDetailViewModel.EndOfGame - same SeriousInjuryEffectKind.LoseAllEquipment
    /// mechanism as Robbed, 36).</summary>
    public static bool ReturnsToWarband(CapturedOutcome outcome) => outcome is CapturedOutcome.Ransomed or CapturedOutcome.Exchanged;

    /// <summary>True for the two outcomes that explicitly kill the warrior (WarriorStatus.Dead) -
    /// false for SoldToSlavers, which maps to WarriorStatus.Retired instead (gone, not confirmed dead).</summary>
    public static bool CausesDeath(CapturedOutcome outcome) => outcome is CapturedOutcome.KilledByUndead or CapturedOutcome.SacrificedByPossessed;
}
