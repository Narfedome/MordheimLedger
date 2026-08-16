using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components;

/// <summary>Reusable "restricted to these warbands" chip editor, factored out of the near-identical block
/// duplicated in EquipmentItemEditDialogViewModel/SkillEditDialogViewModel/MutationEditDialogViewModel
/// (all three edit EquipmentItem/Skill/Mutation.RestrictedToWarbandArchetypeIds the same way). Adds an
/// Exclude mode on top of the original Include-only editor: a rule like Wardog ("every warband except
/// Skaven") used to mean picking and displaying 14 of 15 bands as chips - unmanageably long in the
/// dialog.
///
/// Two independent literal lists (_included/_excluded), never derived from one another DURING editing -
/// explicitly requested by the user after an earlier version auto-computed the Exclude-mode chips as the
/// complement of _included on every toggle: switching to Exclude mode from an unrestricted item (0
/// included) showed EVERY warband as "excluded", the exact wall-of-chips problem this editor exists to
/// avoid. Toggling mode itself never recomputes anything - it only switches which list Add/Remove/
/// DisplayedWarbands operate on. The two lists stay mutually exclusive (adding to one drops the same
/// band from the other) but nothing else keeps them in sync once construction is done - only whichever
/// list matches IsExcludeMode at Save time is actually persisted (see SelectedIds).
///
/// One exception, at construction only: if the item being reopened already carries a genuine partial
/// restriction (some but not all warbands), _excluded is seeded once as the complement of the real saved
/// _included, and the starting mode picked to show fewer chips - reopening "all but Skaven" shows
/// Exclude mode with just Skaven, not 14 Include-mode chips. This is a one-time, faithful read of data
/// that's already saved (nothing to corrupt), fundamentally different from the earlier bug: that one
/// recomputed the complement on every toggle DURING an editing session, including from a fully
/// unrestricted starting point (0 included) where "the complement" is meaningless noise, not real data.
/// Fully unrestricted (0 included) or fully-explicit (every warband included) items get no seeding and
/// start Include/empty, same as before.</summary>
public partial class WarbandRestrictionEditor : ObservableObject
{
    private readonly IReadOnlyList<WarbandArchetype> _allWarbandArchetypes;
    private readonly IWarbandArchetypePickerService _picker;
    private readonly List<WarbandArchetype> _included;
    private readonly List<WarbandArchetype> _excluded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderText))]
    [NotifyPropertyChangedFor(nameof(ToggleModeLabel))]
    private bool isExcludeMode;

    /// <summary>Bound by the ChipListView - a direct view of whichever of _included/_excluded is
    /// currently active, never a computed complement.</summary>
    public ObservableCollection<WarbandArchetype> DisplayedWarbands { get; } = new();

    private List<WarbandArchetype> ActiveList => IsExcludeMode ? _excluded : _included;

    public string HeaderText => ActiveList.Count == 0
        ? LocalizationService.Instance["LibRestrictedToAllHint"]
        : IsExcludeMode
            ? LocalizationService.Instance["LibRestrictedToAllExceptPh"]
            : LocalizationService.Instance["LibRestrictedToWarbandsPh"];

    public string ToggleModeLabel => IsExcludeMode
        ? LocalizationService.Instance["LibRestrictionSwitchToIncludePh"]
        : LocalizationService.Instance["LibRestrictionSwitchToExcludePh"];

    /// <summary>Fires whenever the effective inclusion set could have changed (Add/Remove, not
    /// ToggleMode - toggling switches which list is active but touches neither list's contents) - lets a
    /// caller with a second-level restriction (SkillEditDialogViewModel's RestrictedWarriors, narrowed to
    /// warriors of these bands) prune entries that no longer belong to any included band.</summary>
    public event Action? Changed;

    public WarbandRestrictionEditor(IReadOnlyList<int> restrictedWarbandArchetypeIds, IReadOnlyList<WarbandArchetype> allWarbandArchetypes,
        IWarbandArchetypePickerService picker)
    {
        _allWarbandArchetypes = allWarbandArchetypes;
        _picker = picker;
        _included = allWarbandArchetypes.Where(w => restrictedWarbandArchetypeIds.Contains(w.Id)).ToList();

        // Genuine partial restriction (neither 0 nor every warband) - seed _excluded as its complement
        // ONCE here, a faithful one-time read of the real saved data, and default to whichever mode
        // shows fewer chips. Otherwise (unrestricted, or every warband explicitly included) there's
        // nothing meaningful to derive - _excluded stays empty and Include mode is the honest default.
        if (_included.Count > 0 && _included.Count < allWarbandArchetypes.Count)
        {
            _excluded = allWarbandArchetypes.Where(w => _included.All(i => i.Id != w.Id)).ToList();
            isExcludeMode = _included.Count > allWarbandArchetypes.Count / 2;
        }
        else
        {
            _excluded = new List<WarbandArchetype>();
        }
        RefreshDisplayed();
    }

    private void RefreshDisplayed()
    {
        DisplayedWarbands.Clear();
        foreach (var warband in ActiveList)
            DisplayedWarbands.Add(warband);
    }

    partial void OnIsExcludeModeChanged(bool value) => RefreshDisplayed();

    [RelayCommand]
    private void ToggleMode() => IsExcludeMode = !IsExcludeMode;

    [RelayCommand]
    private async Task Add()
    {
        var picked = await _picker.PickWarbandArchetypesAsync();
        foreach (var warband in picked)
        {
            if (ActiveList.All(w => w.Id != warband.Id))
                ActiveList.Add(warband);
            // Mutually exclusive: a band picked into the active list can't stay in the other one too.
            (IsExcludeMode ? _included : _excluded).RemoveAll(w => w.Id == warband.Id);
        }
        RefreshDisplayed();
        OnPropertyChanged(nameof(HeaderText));
        Changed?.Invoke();
    }

    [RelayCommand]
    private void Remove(WarbandArchetype warband)
    {
        ActiveList.RemoveAll(w => w.Id == warband.Id);
        RefreshDisplayed();
        OnPropertyChanged(nameof(HeaderText));
        Changed?.Invoke();
    }

    /// <summary>Concrete ids to persist on RestrictedToWarbandArchetypeIds - _included directly in
    /// Include mode (already the right semantics), or every known warband minus _excluded in Exclude
    /// mode (computed only here, at read time, never for display). _excluded.Count == 0 is special-cased
    /// to the empty list rather than "every warband minus nothing" - without this, Where(...All(...))
    /// over an empty _excluded is vacuously true for every warband, persisting the entire catalog
    /// explicitly (the exact bug reported: toggle to Exclude, save without excluding anything, every
    /// warband ends up individually "restricted to" instead of "common to all") - HeaderText already
    /// treated an empty ActiveList as "common to all" for display, this just makes Save agree.</summary>
    public List<int> SelectedIds => IsExcludeMode
        ? _excluded.Count == 0 ? new List<int>() : _allWarbandArchetypes.Where(w => _excluded.All(e => e.Id != w.Id)).Select(w => w.Id).ToList()
        : _included.Select(w => w.Id).ToList();
}
