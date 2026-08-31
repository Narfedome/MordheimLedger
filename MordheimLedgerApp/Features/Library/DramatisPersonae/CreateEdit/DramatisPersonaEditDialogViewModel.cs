using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

public partial class DramatisPersonaEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, DramatisPersonaHireFeeKind> _feeKindByLabel = new();
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private readonly IDetailDialogService _detailDialogs;
    private readonly ILibraryService _libraryService;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private DramatisPersona item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur - même mécanisme que HiredSwordEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    /// <summary>Mouvement en édition - toujours un entier ici (pas de MovementOverride pour
    /// DramatisPersona), même idiome que HiredSwordEditDialogViewModel.MovementInput.</summary>
    [ObservableProperty]
    private string movementInput;

    public ObservableCollection<string> FeeKindOptions { get; } = new();

    [ObservableProperty]
    private string selectedFeeKindLabel = string.Empty;

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même Include/Exclude editor que HiredSwordEditDialogViewModel.WarbandRestriction.</summary>
    public WarbandRestrictionEditor WarbandRestriction { get; }

    /// <summary>Chaque règle propre (réutilisée ou unique à ce personnage, mais toujours un effet
    /// simple/nommable) - les systèmes à embranchements/étapes multiples restent en texte libre dans
    /// Description, voir DramatisPersona.SpecialRules.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>Équipement de départ fixe (ex. Aenur : Armure d'Ithilmar/Cape Elfique/Ienh-Khain) - édité
    /// en mémoire ici, recopié sur Item.StartingEquipmentIds à la sauvegarde, même principe que
    /// HiredSwordEditDialogViewModel.StartingEquipment.</summary>
    public ObservableCollection<EquipmentItem> StartingEquipment { get; }

    /// <summary>Compétences déjà connues (ex. Aenur : Frapper pour Blesser...) - liste FIXE, pas un choix
    /// de catégories à la Progression (contrairement à HiredSword.AllowedSkillCategories) - voir
    /// DramatisPersona.Skills.</summary>
    public ObservableCollection<Skill> Skills { get; }

    /// <summary>0 ou 1 élément (voir DramatisPersona.MagicSchoolId), même principe que
    /// HiredSwordEditDialogViewModel.MagicSchools.</summary>
    public ObservableCollection<MagicSchool> MagicSchools { get; }

    public DramatisPersonaEditDialogViewModel(DramatisPersona item, string title, IWarbandArchetypePickerService warbandPicker,
        IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker, ISpecialRulePickerService specialRulePicker,
        IMagicSchoolPickerService magicSchoolPicker, IDetailDialogService detailDialogs, ILibraryService libraryService,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes, IReadOnlyList<EquipmentItem> initialStartingEquipment)
    {
        this.item = item;
        this.title = title;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        _detailDialogs = detailDialogs;
        _libraryService = libraryService;
        movementInput = item.Movement.ToString();

        foreach (var kind in Enum.GetValues<DramatisPersonaHireFeeKind>())
        {
            var label = Loc[$"DramatisPersonaFeeKind{kind}"];
            _feeKindByLabel[label] = kind;
            FeeKindOptions.Add(label);
        }
        selectedFeeKindLabel = Loc[$"DramatisPersonaFeeKind{item.FeeKind}"];

        WarbandRestriction = new WarbandRestrictionEditor(item.RestrictedToWarbandArchetypeIds, allWarbandArchetypes, warbandPicker);
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
        StartingEquipment = new ObservableCollection<EquipmentItem>(initialStartingEquipment);
        Skills = new ObservableCollection<Skill>(item.Skills);
        MagicSchools = new ObservableCollection<MagicSchool>(item.MagicSchool is { } school ? new[] { school } : Array.Empty<MagicSchool>());
    }

    partial void OnSelectedFeeKindLabelChanged(string value)
    {
        if (_feeKindByLabel.TryGetValue(value, out var kind))
            Item.FeeKind = kind;
    }

    // Catalogue non filtré (null) - même choix que HiredSwordEditDialogViewModel.AddSpecialRule : une
    // règle propre à un Dramatis Persona n'est attachée qu'à DramatisPersonaSpecialRuleEntity.
    [RelayCommand]
    private async Task AddSpecialRule()
    {
        var picked = await _specialRulePicker.PickSpecialRulesAsync();
        foreach (var rule in picked)
        {
            if (SpecialRules.Any(r => r.Id == rule.Id)) continue;
            SpecialRules.Add(rule);
        }
    }

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule);

    [RelayCommand]
    private void RemoveSpecialRule(SpecialRule rule) => SpecialRules.Remove(rule);

    // warbandArchetypeId: 0 - aucune bande réelle n'est concernée par l'équipement de départ d'un
    // Dramatis Persona (jamais recruté par bande), même choix que HiredSwordEditDialogViewModel.
    [RelayCommand]
    private async Task AddStartingEquipment()
    {
        var picked = await _equipmentPicker.PickEquipmentAsync(warbandArchetypeId: 0);
        foreach (var equipmentItem in picked)
        {
            if (StartingEquipment.Any(i => i.Id == equipmentItem.Id)) continue;
            StartingEquipment.Add(equipmentItem);
        }
    }

    [RelayCommand]
    private Task ShowStartingEquipmentDetail(EquipmentItem equipmentItem) => _detailDialogs.ShowEquipmentDetailDialogAsync(equipmentItem);

    [RelayCommand]
    private void RemoveStartingEquipment(EquipmentItem equipmentItem) => StartingEquipment.Remove(equipmentItem);

    // Même raison que AddStartingEquipment : pas de bande réelle à narrower ici.
    [RelayCommand]
    private async Task AddSkill()
    {
        var picked = await _skillPicker.PickSkillAsync(warbandArchetypeId: 0);
        foreach (var skill in picked)
        {
            if (Skills.Any(s => s.Id == skill.Id)) continue;
            Skills.Add(skill);
        }
    }

    [RelayCommand]
    private Task ShowSkillDetail(Skill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill);

    [RelayCommand]
    private void RemoveSkill(Skill skill) => Skills.Remove(skill);

    /// <summary>Remplace l'entrée existante plutôt que d'empiler (un seul FK) - même picker multi-
    /// sélection que HiredSwordEditDialogViewModel.AddMagicSchool, seul le premier résultat est retenu.</summary>
    [RelayCommand]
    private async Task AddMagicSchool()
    {
        var picked = await _magicSchoolPicker.PickMagicSchoolsAsync();
        if (picked.FirstOrDefault() is not { } school) return;

        MagicSchools.Clear();
        MagicSchools.Add(school);
    }

    [RelayCommand]
    private async Task ShowMagicSchoolDetail(MagicSchool school)
    {
        var language = LocalizationService.Instance.Language;
        var spells = (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == school.Id).ToList();
        await ShowChipDetailAsync(school.Name, school.Description, spells);
    }

    [RelayCommand]
    private void RemoveMagicSchool(MagicSchool school) => MagicSchools.Remove(school);

    private bool ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Item.Name))
        {
            NameError = Loc["LibFieldRequired"];
            return false;
        }
        NameError = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateRequiredFields()) return;

        if (int.TryParse(MovementInput, out var movement)) Item.Movement = movement;
        Item.RestrictedToWarbandArchetypeIds = WarbandRestriction.SelectedIds;
        Item.SpecialRules = SpecialRules.ToList();
        Item.StartingEquipmentIds = StartingEquipment.Select(e => e.Id).ToList();
        Item.Skills = Skills.ToList();
        Item.MagicSchool = MagicSchools.FirstOrDefault();
        Item.MagicSchoolId = Item.MagicSchool?.Id;
        Close(true);
    }
}
