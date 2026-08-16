using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components;

/// <summary>Read-only counterpart to WarbandRestrictionEditor, used by the 3 XxxDetailDialogViewModels
/// (Equipment/Skill/Mutation) instead of the editable Include/Exclude toggle - a recap dialog never
/// edits the restriction, it just needs the same "don't render a dozen-plus chips" collapse applied to
/// whatever list the caller already resolved (e.g. Wardog's "all warbands except Skaven"). Unlike the
/// Edit dialog's WarbandRestrictionEditor (which deliberately never auto-recomputes, to avoid silently
/// persisting the wrong thing if the user toggles modes without touching either list and saves), this is
/// a pure read-only computation with no save path - there's nothing to corrupt, so collapsing to the
/// complement here is safe purely as a display choice, recomputed fresh from the real saved data every
/// time the dialog opens.</summary>
public static class WarbandRestrictionDisplay
{
    /// <summary>True once the restriction literally names every known warband - normalizes what would
    /// otherwise render as "all except (nothing)" back to plain "common to all".</summary>
    private static bool IsFullyIncluded(IReadOnlyList<WarbandArchetype> restrictedWarbands, IReadOnlyList<WarbandArchetype> allWarbandArchetypes) =>
        allWarbandArchetypes.Count > 0 && restrictedWarbands.Count == allWarbandArchetypes.Count;

    private static bool CollapsesToExcluded(IReadOnlyList<WarbandArchetype> restrictedWarbands, IReadOnlyList<WarbandArchetype> allWarbandArchetypes) =>
        allWarbandArchetypes.Count > 0 && restrictedWarbands.Count > allWarbandArchetypes.Count / 2 && !IsFullyIncluded(restrictedWarbands, allWarbandArchetypes);

    public static string HeaderTextFor(IReadOnlyList<WarbandArchetype> restrictedWarbands, IReadOnlyList<WarbandArchetype> allWarbandArchetypes) =>
        restrictedWarbands.Count == 0 || IsFullyIncluded(restrictedWarbands, allWarbandArchetypes)
            ? LocalizationService.Instance["LibRestrictedToAllHint"]
            : CollapsesToExcluded(restrictedWarbands, allWarbandArchetypes)
                ? LocalizationService.Instance["LibRestrictedToAllExceptPh"]
                : LocalizationService.Instance["LibRestrictedToWarbandsPh"];

    public static List<WarbandArchetype> DisplayedFor(IReadOnlyList<WarbandArchetype> restrictedWarbands, IReadOnlyList<WarbandArchetype> allWarbandArchetypes) =>
        CollapsesToExcluded(restrictedWarbands, allWarbandArchetypes)
            ? allWarbandArchetypes.Where(w => restrictedWarbands.All(r => r.Id != w.Id)).ToList()
            : restrictedWarbands.ToList();
}
