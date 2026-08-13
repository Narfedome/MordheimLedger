using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

/// <summary>Read-only recap of EquipmentItemEditDialog. Unlike the Edit dialog (no UI for
/// RestrictedToWarriorArchetypeIds - seed-only, see EquipmentItem), the recap does surface it: it's
/// real data on the model, just not editable here.</summary>
public partial class EquipmentItemDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public EquipmentItem Item { get; }
    public string CategoryLabel { get; }
    public string RarityDisplay { get; }

    /// <summary>"30 - 48" when CostRandomMax is set (worst-case total, see EquipmentItem.CostRandomMax),
    /// otherwise just "30".</summary>
    public string CostDisplay => Item.CostRandomMax is { } max ? $"{Item.Cost} - {Item.Cost + max}" : Item.Cost.ToString();

    /// <summary>Already resolved by the caller (EquipmentItemViewModel.ShowDetails) from the ids on
    /// Item - same idiom as SkillViewModel.Edit's initialWarriors fetch.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }
    public List<WarriorArchetype> RestrictedWarriors { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice affichés en même temps liste vide -
    /// même principe que EquipmentItemEditDialogViewModel.RestrictedWarbandsHeaderText.</summary>
    public string RestrictedWarbandsHeaderText =>
        RestrictedWarbands.Count > 0 ? Loc["LibRestrictedToWarbandsPh"] : Loc["LibRestrictedToAllHint"];

    /// <summary>Item.SpecialRules (intrinsic to the catalog entry) plus the material chosen for THIS
    /// specific purchase (Gromril/Ithilmar...), when any - see WarbandEditDialogViewModel/
    /// WarriorEditDialogViewModel.ShowEquipmentDetail. Item.SpecialRules itself is left untouched (the
    /// material is a per-purchase choice, not part of the shared catalog entry) - this is a display-only
    /// merge, same idiom as EquipmentPick.Name/WarriorEquipment.NameDisplay for the abbreviated chip.</summary>
    public List<SpecialRule> DisplayedSpecialRules { get; }

    public EquipmentItemDetailDialogViewModel(EquipmentItem item, string categoryLabel,
        List<WarbandArchetype> restrictedWarbands, List<WarriorArchetype> restrictedWarriors, SpecialRule? materialRule = null)
    {
        Item = item;
        Title = materialRule?.Abbreviation is { Length: > 0 } abbr ? $"{item.Name} ({abbr})" : item.Name;
        CategoryLabel = categoryLabel;
        RarityDisplay = item.Rarity?.ToString() ?? Loc["LibFilterCommon"];
        RestrictedWarbands = restrictedWarbands;
        RestrictedWarriors = restrictedWarriors;
        DisplayedSpecialRules = materialRule is null ? item.SpecialRules : item.SpecialRules.Append(materialRule).ToList();
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowWarriorDetail(WarriorArchetype warrior) => ShowChipDetailAsync(warrior.Name, warrior.Description);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);
}
