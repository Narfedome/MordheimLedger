using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

/// <summary>Read-only recap of DramatisPersonaEditDialog.</summary>
public partial class DramatisPersonaDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public DramatisPersona Item { get; }

    /// <summary>Same reason StatRowView.MovementText needs an explicit string - DramatisPersona has no
    /// MovementOverride concept, so this is just Item.Movement as text.</summary>
    public string MovementDisplay => Item.Movement.ToString();

    public string FeeKindDisplay => Loc[$"DramatisPersonaFeeKind{Item.FeeKind}"];

    public bool HasHireCost => Item.HireCost is not null;
    public bool HasUpkeep => Item.Upkeep is not null;

    /// <summary>0 ou 1 élément (voir DramatisPersona.MagicSchoolId) - ChipListView masque toute la
    /// section (header inclus) si vide.</summary>
    public List<MagicSchool> MagicSchools => Item.MagicSchool is { } school ? new List<MagicSchool> { school } : new List<MagicSchool>();

    /// <summary>Déjà résolu par l'appelant (DetailDialogService.ShowDramatisPersonaDetailDialogAsync),
    /// même principe que HiredSwordDetailDialogViewModel.StartingEquipment.</summary>
    public List<EquipmentItem> StartingEquipment { get; }

    /// <summary>Collapsed to its complement against allWarbandArchetypes when it covers more than half
    /// the catalog - see WarbandRestrictionDisplay.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }
    public string RestrictedWarbandsHeaderText { get; }

    /// <summary>Sorts de l'école du personnage (si lanceur de sorts) - déjà résolus par l'appelant
    /// (DetailDialogService.ShowDramatisPersonaDetailDialogAsync), même principe que
    /// HiredSwordDetailDialogViewModel.</summary>
    private readonly List<Spell> _magicSchoolSpells;
    private readonly IDetailDialogService _detailDialogs;

    public DramatisPersonaDetailDialogViewModel(DramatisPersona item, List<EquipmentItem> startingEquipment,
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

    [RelayCommand]
    private Task ShowStartingEquipmentDetail(EquipmentItem equipmentItem) => _detailDialogs.ShowEquipmentDetailDialogAsync(equipmentItem);

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private Task ShowSkillDetail(Skill skill) => ShowChipDetailAsync(skill.Name, skill.Description);

    [RelayCommand]
    private Task ShowMagicSchoolDetail(MagicSchool school) => ShowChipDetailAsync(school.Name, school.Description, _magicSchoolSpells);
}
