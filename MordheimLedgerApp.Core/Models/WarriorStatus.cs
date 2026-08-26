namespace MordheimLedgerApp.Core.Models;

/// <summary>
/// Long-term roster status. Deliberately coarse: the specific outcome of a Serious Injury roll
/// (multiple injuries, captured, etc.) is recorded via Warrior.Injuries (see WarbandDetailViewModel.
/// EndOfGame) rather than modeled here — see the roadmap's "no rules engine in V1" decision.
/// </summary>
public enum WarriorStatus
{
    Active = 0,
    Dead = 1,

    /// <summary>Missed the next game through sickness/injury outside the Serious Injury table (e.g.
    /// the Exploration chart's "Puits" result) - deliberately not permanent like Dead: cleared
    /// automatically the next time the End of Game wizard runs for this warband (see
    /// WarbandDetailViewModel.EndOfGame), representing that the missed game has now happened. Still
    /// shown on the roster in the meantime as a reminder not to field the warrior.</summary>
    Sick = 2,

    /// <summary>Permanently out of the active roster but never fought their last battle - unlike Dead,
    /// forced by a specific rule rather than a death roll (currently only "Blinded in One Eye" losing
    /// its second eye, see Core.Rules.SeriousInjuryEffectKind.ForcedRetirement). Treated identically to
    /// Dead everywhere the roster excludes inactive warriors (WarbandDetailViewModel's Heroes/Henchmen/
    /// Rating, WarbandEditDialogViewModel's existing-warrior slots) - kept in the roster for band
    /// history rather than deleted, same as Dead.</summary>
    Retired = 3
}
