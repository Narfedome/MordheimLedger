using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>One chip-worth of the roster card's "Haine" list (Rancune injury results, see
/// Models.WarriorHatred) - wraps the resolved target name with the "Haine : {0}" prefix (same split as
/// SpecialRuleChip: Core/the model stay localization-free, the prefix is applied here in the App
/// layer). ChipView binds its Label straight to Name.</summary>
public sealed class WarriorHatredChip
{
    public required WarriorHatred Item { get; init; }
    public required string Name { get; init; }
}
