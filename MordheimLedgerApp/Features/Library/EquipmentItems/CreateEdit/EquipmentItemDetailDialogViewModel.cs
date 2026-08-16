using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

/// <summary>Read-only recap of EquipmentItemEditDialog. Unlike the Edit dialog (no UI for
/// RestrictedToWarriorArchetypeIds - seed-only, see EquipmentItem), the recap does surface it: it's
/// real data on the model, just not editable here.</summary>
public partial class EquipmentItemDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public EquipmentItem Item { get; }
    public string CategoryLabel { get; }
    public string RarityDisplay { get; }

    /// <summary>Shows the StatRowView profile block only for a mount (Category == Animal) - meaningless
    /// for any other category. See EquipmentItemEditDialogViewModel's editable counterpart.</summary>
    public bool IsAnimalCategory => Item.Category == EquipmentCategory.Animal;

    /// <summary>"30 - 48" when CostRandomMax is set (worst-case total, see EquipmentItem.CostRandomMax),
    /// otherwise just "30".</summary>
    public string CostDisplay => Item.CostRandomMax is { } max ? $"{Item.Cost} - {Item.Cost + max}" : Item.Cost.ToString();

    /// <summary>Already resolved by the caller (EquipmentItemViewModel.ShowDetails) from the ids on
    /// Item - same idiom as SkillViewModel.Edit's initialWarriors fetch. Collapsed to its complement
    /// against allWarbandArchetypes when it covers more than half the catalog (e.g. Wardog's "all but
    /// Skaven") - see WarbandRestrictionDisplay. Safe here (unlike the Edit dialog's
    /// WarbandRestrictionEditor, which deliberately never does this) because a read-only recap has no
    /// save path to accidentally corrupt - this is purely a fresh display computation every time.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }
    public List<WarriorArchetype> RestrictedWarriors { get; }

    /// <summary>Reflects whichever of Include/Exclude RestrictedWarbands ended up collapsed to - see
    /// WarbandRestrictionDisplay.HeaderTextFor.</summary>
    public string RestrictedWarbandsHeaderText { get; }

    /// <summary>Item.SpecialRules (intrinsic to the catalog entry) plus the material chosen for THIS
    /// specific purchase (Gromril/Ithilmar...), when any - see WarbandEditDialogViewModel/
    /// WarriorEditDialogViewModel.ShowEquipmentDetail. Item.SpecialRules itself is left untouched (the
    /// material is a per-purchase choice, not part of the shared catalog entry) - this is a display-only
    /// merge, same idiom as EquipmentPick.Name/WarriorEquipment.NameDisplay for the abbreviated chip.</summary>
    public List<SpecialRule> DisplayedSpecialRules { get; }

    private readonly IDetailDialogService _detailDialogs;

    public EquipmentItemDetailDialogViewModel(EquipmentItem item, string categoryLabel, List<WarbandArchetype> restrictedWarbands,
        List<WarbandArchetype> allWarbandArchetypes, List<WarriorArchetype> restrictedWarriors, IDetailDialogService detailDialogs,
        SpecialRule? materialRule = null)
    {
        Item = item;
        Title = materialRule?.Abbreviation is { Length: > 0 } abbr ? $"{item.Name} ({abbr})" : item.Name;
        CategoryLabel = categoryLabel;
        RarityDisplay = item.Rarity?.ToString() ?? Loc["LibFilterCommon"];
        RestrictedWarbands = WarbandRestrictionDisplay.DisplayedFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarbandsHeaderText = WarbandRestrictionDisplay.HeaderTextFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarriors = restrictedWarriors;
        DisplayedSpecialRules = materialRule is null ? item.SpecialRules : item.SpecialRules.Append(materialRule).ToList();
        _detailDialogs = detailDialogs;
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowWarriorDetail(WarriorArchetype warrior) => ShowChipDetailAsync(warrior.Name, warrior.Description);

    /// <summary>Material rules (CostMultiplier/Rarity set, e.g. Gromril/Ithilmar) get the full
    /// SpecialRuleDetailDialog recap instead of the generic Nom+Description popup, so the Rare rating and
    /// price multiplier are visible too - ordinary rules keep the lighter ChipDetailDialog, same as
    /// every other special-rule chip in the app.</summary>
    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) =>
        rule.CostMultiplier.HasValue || rule.Rarity.HasValue
            ? _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule)
            : ShowChipDetailAsync(rule.Name, rule.Description);
}
