using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Find-or-create-by-roll matching against the Injury catalog (Injuries.json), shared between
/// WarbandDetailViewModel.EndOfGame (resolving which catalog Injury to actually attach at Save) and
/// WarriorOutcomeRow (previewing the same resolution live in the wizard, e.g. to show a tappable
/// SpecialRule chip for Madness/24 before the player even hits Save). Extracted 2026-08-25 rather than
/// duplicated - the roll-range parsing is a real small algorithm, not "three similar lines".</summary>
internal static class InjuryCatalogLookup
{
    /// <summary>Does <paramref name="roll"/> fall within <paramref name="rollRange"/>, the catalog's
    /// free-text display field (Injury.RollRange/BranchRange - see Injuries.json, ex. "22", "16, 21",
    /// "11-15", "62-63")? Tolerant parser: comma-separated tokens, each a lone number or an "a-b" range.</summary>
    public static bool RollRangeMatches(string? rollRange, int roll)
    {
        if (string.IsNullOrWhiteSpace(rollRange)) return false;

        foreach (var token in rollRange.Split(',', StringSplitOptions.TrimEntries))
        {
            var bounds = token.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length == 2 && int.TryParse(bounds[0], out var lo) && int.TryParse(bounds[1], out var hi))
            {
                if (roll >= lo && roll <= hi) return true;
            }
            else if (int.TryParse(token, out var exact) && exact == roll)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the catalog Injury for a given roll/branch, or null if none matches (a roll
    /// outside the official table, or a branching roll with no branchSubRoll given and no generic
    /// fallback row). branchSubRoll null retrieves the generic (BranchRange empty) row if one exists,
    /// else an arbitrary branch row - same fallback used by GetOrCreateInjuryAsync for the untracked
    /// "Blessures multiples" nested-branch case.</summary>
    public static Injury? Find(IReadOnlyList<Injury> catalog, InjuryCategory category, int roll, int? branchSubRoll)
    {
        var candidates = catalog.Where(i => i.Category == category && RollRangeMatches(i.RollRange, roll)).ToList();
        return branchSubRoll is { } sub
            ? candidates.FirstOrDefault(i => RollRangeMatches(i.BranchRange, sub))
            : candidates.FirstOrDefault(i => string.IsNullOrWhiteSpace(i.BranchRange)) ?? candidates.FirstOrDefault();
    }
}
