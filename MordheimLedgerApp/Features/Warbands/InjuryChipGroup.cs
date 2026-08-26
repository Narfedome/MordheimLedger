using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>One chip-worth of the roster card's "Blessures" list - groups every WarriorInjury sharing
/// the same catalog Injury (e.g. two separate "Vieille blessure" results) into a single chip labeled
/// "Vieille blessure x2" instead of one chip per instance (user feedback 2026-08-27). Representative is
/// an arbitrary member of the group - fine since ShowInjuryDetail only needs its Item (the catalog row,
/// identical for every member of the group) to open the detail popup. See WarbandDetailViewModel.ToRow
/// for the grouping (and the filtering of SeriousInjuryTable.HidesRosterChip results before it).</summary>
public sealed class InjuryChipGroup
{
    public required WarriorInjury Representative { get; init; }
    public required int Count { get; init; }
    public string Name => Count > 1 ? $"{Representative.Name} x{Count}" : Representative.Name;
}
