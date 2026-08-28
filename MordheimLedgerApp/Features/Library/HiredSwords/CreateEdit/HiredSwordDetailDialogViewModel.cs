using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;

/// <summary>Read-only recap of HiredSwordEditDialog.</summary>
public partial class HiredSwordDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public HiredSword Item { get; }

    /// <summary>Same reason StatRowView.MovementText needs an explicit string - HiredSword has no
    /// MovementOverride concept (unlike WarriorArchetype), so this is just Item.Movement as text.</summary>
    public string MovementDisplay => Item.Movement.ToString();

    public string AllowedSkillCategoriesText => Item.AllowedSkillCategories.Count == 0
        ? Loc["HiredSwordNoAllowedSkillCategories"]
        : string.Join(", ", Item.AllowedSkillCategories.Select(c => Loc[$"SkillCategory{c}"]));

    /// <summary>0 ou 1 élément (voir HiredSword.MagicSchoolId) - ChipListView masque toute la section
    /// (header inclus) si vide, aucun élément à montrer pour un Franc-Tireur qui n'est pas lanceur de
    /// sorts (l'immense majorité).</summary>
    public List<MagicSchool> MagicSchools => Item.MagicSchool is { } school ? new List<MagicSchool> { school } : new List<MagicSchool>();

    /// <summary>Already resolved by the caller (DetailDialogService.ShowHiredSwordDetailDialogAsync).</summary>
    public List<EquipmentItem> StartingEquipment { get; }

    /// <summary>Collapsed to its complement against allWarbandArchetypes when it covers more than half
    /// the catalog - see WarbandRestrictionDisplay.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }
    public string RestrictedWarbandsHeaderText { get; }

    public HiredSwordDetailDialogViewModel(HiredSword item, List<EquipmentItem> startingEquipment,
        List<WarbandArchetype> restrictedWarbands, List<WarbandArchetype> allWarbandArchetypes)
    {
        Item = item;
        Title = item.Name;
        StartingEquipment = startingEquipment;
        RestrictedWarbands = WarbandRestrictionDisplay.DisplayedFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarbandsHeaderText = WarbandRestrictionDisplay.HeaderTextFor(restrictedWarbands, allWarbandArchetypes);
    }

    [RelayCommand]
    private Task ShowStartingEquipmentDetail(EquipmentItem equipmentItem) => ShowChipDetailAsync(equipmentItem.Name, equipmentItem.Description);

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private Task ShowMagicSchoolDetail(MagicSchool school) => ShowChipDetailAsync(school.Name, school.Description);
}
