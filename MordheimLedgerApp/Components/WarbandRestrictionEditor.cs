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
/// Two independent literal lists (_included/_excluded), never derived from one another - explicitly
/// requested by the user after an earlier version auto-computed the Exclude-mode chips as the complement
/// of _included against every known warband: toggling to Exclude mode from an unrestricted item (0
/// included) showed EVERY warband as "excluded", the exact wall-of-chips problem this editor exists to
/// avoid. Now toggling mode never recomputes anything - it only switches which list Add/Remove/
/// DisplayedWarbands operate on. _excluded always starts empty and is fed solely by the "+" picker; an
/// existing "all but Skaven" item reopened in Exclude mode won't pre-show Skaven, the user re-adds it -
/// a deliberate simplicity trade-off (this UI mainly authors NEW "all except X" rules from scratch; seed
/// content like Wardog is authored via RestrictedToWarbandNames in JSON instead, never through here).
/// The two lists stay mutually exclusive (adding to one drops the same band from the other) but nothing
/// else keeps them in sync - only whichever list matches IsExcludeMode at Save time is actually
/// persisted (see SelectedIds).</summary>
public partial class WarbandRestrictionEditor : ObservableObject
{
    private readonly IReadOnlyList<WarbandArchetype> _allWarbandArchetypes;
    private readonly IWarbandArchetypePickerService _picker;
    private readonly List<WarbandArchetype> _included;
    private readonly List<WarbandArchetype> _excluded = new();

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
        // isExcludeMode starts false and _excluded starts empty - always Include mode, mirroring
        // RestrictedToWarbandArchetypeIds' own inclusion semantics verbatim, no guessing.
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
    /// mode (computed only here, at read time, never for display) - an empty _excluded correctly
    /// persists as the empty list ("common to all"), not as every warband explicitly listed.</summary>
    public List<int> SelectedIds => IsExcludeMode
        ? _allWarbandArchetypes.Where(w => _excluded.All(e => e.Id != w.Id)).Select(w => w.Id).ToList()
        : _included.Select(w => w.Id).ToList();
}
