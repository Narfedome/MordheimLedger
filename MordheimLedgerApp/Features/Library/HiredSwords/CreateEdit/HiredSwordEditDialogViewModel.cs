using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;

/// <summary>One checkbox row of the "Compétences autorisées" block - no existing reusable control
/// covers this fixed 6-entry list (see WarriorArchetypeEditDialog.xaml, which has the same model field
/// but no editor UI for it), so a small per-dialog helper class is enough.</summary>
public partial class SkillCategoryOption : ObservableObject
{
    public SkillCategory Category { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool isChecked;

    public SkillCategoryOption(SkillCategory category, string label, bool isChecked)
    {
        Category = category;
        Label = label;
        this.isChecked = isChecked;
    }
}

public partial class HiredSwordEditDialogViewModel : DialogViewModel<bool>
{
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IDetailDialogService _detailDialogs;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private HiredSword item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    /// <summary>Mouvement en édition - toujours un entier ici (pas d'équivalent MovementOverride pour
    /// HiredSword), même idiome que WarriorArchetypeEditDialogViewModel.MovementInput en plus simple.</summary>
    [ObservableProperty]
    private string movementInput;

    /// <summary>Une case par SkillCategory - repliées dans Item.AllowedSkillCategories au Save().</summary>
    public ObservableCollection<SkillCategoryOption> SkillCategoryOptions { get; }

    /// <summary>Équipement de départ fixe (ex. Franc-Tireur : Morgenstern/Casque/Gantelet à pointes) -
    /// édité en mémoire ici, recopié sur Item.StartingEquipmentIds à la sauvegarde, même principe que
    /// EquipmentListEditDialogViewModel.Items.</summary>
    public ObservableCollection<EquipmentItem> StartingEquipment { get; }

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même Include/Exclude editor que SkillEditDialogViewModel.WarbandRestriction.</summary>
    public WarbandRestrictionEditor WarbandRestriction { get; }

    /// <summary>Règles spéciales propres à ce Franc-Tireur (ex. Tueur de Troll : Deathwish/Hard to Kill)
    /// - édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde, même principe que
    /// WarriorArchetypeEditDialogViewModel.SpecialRules.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    public HiredSwordEditDialogViewModel(HiredSword item, string title, IWarbandArchetypePickerService warbandPicker,
        IEquipmentPickerService equipmentPicker, ISpecialRulePickerService specialRulePicker, IDetailDialogService detailDialogs,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes, IReadOnlyList<EquipmentItem> initialStartingEquipment)
    {
        this.item = item;
        this.title = title;
        _equipmentPicker = equipmentPicker;
        _specialRulePicker = specialRulePicker;
        _detailDialogs = detailDialogs;
        movementInput = item.Movement.ToString();

        SkillCategoryOptions = new ObservableCollection<SkillCategoryOption>(
            Enum.GetValues<SkillCategory>().Select(c =>
                new SkillCategoryOption(c, Loc[$"SkillCategory{c}"], item.AllowedSkillCategories.Contains(c))));

        StartingEquipment = new ObservableCollection<EquipmentItem>(initialStartingEquipment);
        WarbandRestriction = new WarbandRestrictionEditor(item.RestrictedToWarbandArchetypeIds, allWarbandArchetypes, warbandPicker);
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
    }

    [RelayCommand]
    private async Task AddStartingEquipment()
    {
        // warbandArchetypeId: 0 - aucune bande réelle n'est concernée par l'équipement de départ d'un
        // Franc-Tireur (jamais recruté), ce qui limite le picker aux objets communs/non-restreints -
        // suffisant pour Morgenstern/Casque/Gantelet à pointes, tous non-restreints.
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

    // Catalogue non filtré (null) - même choix que les callers EquipmentItem, une règle propre à un
    // Franc-Tireur (ex. "Deathwish") n'est attachée qu'à HiredSwordSpecialRuleEntity, jamais à
    // WarriorArchetypeSpecialRuleEntity, donc SpecialRuleFilterKind.Warrior la masquerait à tort.
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
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private void RemoveSpecialRule(SpecialRule rule) => SpecialRules.Remove(rule);

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
        Item.AllowedSkillCategories = SkillCategoryOptions.Where(o => o.IsChecked).Select(o => o.Category).ToList();
        Item.StartingEquipmentIds = StartingEquipment.Select(e => e.Id).ToList();
        Item.RestrictedToWarbandArchetypeIds = WarbandRestriction.SelectedIds;
        Item.SpecialRules = SpecialRules.ToList();
        Close(true);
    }
}
