namespace MordheimLedgerApp.Core.Models;

/// <summary>
/// Long-term roster status. Deliberately coarse: the specific outcome of a Serious Injury roll
/// (multiple injuries, captured, etc.) is recorded as free text (see Warrior.Notes) rather than
/// modeled here — see the roadmap's "no rules engine in V1" decision.
/// </summary>
public enum WarriorStatus
{
    Active = 0,
    Dead = 1
}
