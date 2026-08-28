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

    /// <summary>Sorts de l'école du Franc-Tireur (si lanceur de sorts) - déjà résolus par l'appelant
    /// (DetailDialogService.ShowHiredSwordDetailDialogAsync), même principe que StartingEquipment.
    /// Vide pour l'immense majorité des Francs-Tireurs (pas de MagicSchool). Passé à ShowChipDetailAsync
    /// (surcharge 3 args) plutôt que Nom+Description seul - même appel que WarbandArchetypeDetailDialog/
    /// EditDialogViewModel.ShowMagicSchoolDetail.</summary>
    private readonly List<Spell> _magicSchoolSpells;
    private readonly IDetailDialogService _detailDialogs;

    public HiredSwordDetailDialogViewModel(HiredSword item, List<EquipmentItem> startingEquipment,
        List<WarbandArchetype> restrictedWarbands, List<WarbandArchetype> allWarbandArchetypes, List<Spell> magicSchoolSpells,
        IDetailDialogService detailDialogs)
    {
        Item = item;
        Title = item.Name;
        StartingEquipment = startingEquipment;
        RestrictedWarbands = WarbandRestrictionDisplay.DisplayedFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarbandsHeaderText = WarbandRestrictionDisplay.HeaderTextFor(restrictedWarbands, allWarbandArchetypes);
        _magicSchoolSpells = magicSchoolSpells;
        _detailDialogs = detailDialogs;
    }

    /// <summary>Recap complet (coût/rareté/restrictions/règles spéciales cliquables), pas le popup
    /// Nom+Description générique - même appel qu'EquipmentListDetailDialogViewModel.ShowItemDetail, le
    /// seul autre endroit qui montre des chips EquipmentItem dans un recap en lecture seule. Contrairement
    /// aux chips "restriction" (bandes/écoles) qui restent sur le popup générique, un objet d'équipement a
    /// des attributs propres qu'un simple Nom+Description ne montre pas.</summary>
    [RelayCommand]
    private Task ShowStartingEquipmentDetail(EquipmentItem equipmentItem) => _detailDialogs.ShowEquipmentDetailDialogAsync(equipmentItem);

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private Task ShowMagicSchoolDetail(MagicSchool school) => ShowChipDetailAsync(school.Name, school.Description, _magicSchoolSpells);
}
