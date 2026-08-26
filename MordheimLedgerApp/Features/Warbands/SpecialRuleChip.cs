using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>One chip-worth of "Règles spéciales" on the roster card. Usually a 1:1 wrapper around a
/// SpecialRule (Name is just the catalog Name), but a Hatred-granting rule (SpecialRule.
/// HatredTargetWarbandArchetypeIds non-empty) explodes into one SpecialRuleChip per target instead of
/// a single generic "Haine" chip - see WarbandDetailViewModel.ToRow. ChipView binds its Label straight
/// to Name (same contract as WarriorInjury/WarriorSkill's Name passthrough), and the tap-to-detail
/// command still opens the real catalog Item, description unchanged.</summary>
public sealed class SpecialRuleChip
{
    public required SpecialRule Item { get; init; }
    public required string Name { get; init; }
}
