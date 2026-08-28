using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>One chip-worth of the roster card's "Haine" list - wraps the resolved target name with the
/// "Haine : {0}" prefix (same split as SpecialRuleChip: Core/the model stay localization-free, the
/// prefix is applied here in the App layer). ChipView binds its Label straight to Name.
///
/// Two sources feed this same list (unified 2026-08-28, user request - "on devrait garder ces chip pour
/// les haine meme donnée par les regle special"): a Rancune Serious Injury result (Item set, a real
/// Models.WarriorHatred row) and a SpecialRule with a mechanized Hatred target (Item null - see
/// WarbandDetailViewModel.BuildRuleHatredChips, exploded the same way SpecialRuleChip already explodes
/// SpecialRule.HatredTargetWarbandArchetypeIds for the Règles spéciales section - this list just mirrors
/// those same targets here too). Tapping either kind opens the same generic rulebook Hatred recap
/// (ShowHatredDetail) rather than a per-source dialog, so Item isn't read by the click handler - it's
/// kept only for a future per-row need, not required today.</summary>
public sealed class WarriorHatredChip
{
    public WarriorHatred? Item { get; init; }
    public required string Name { get; init; }
}
