using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

public partial class WarriorArchetypeEditDialogViewModel : DialogViewModel<bool>
{
    private const int StepCount = 3;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly Dictionary<string, EquipmentList> _equipmentListByLabel = new();

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarriorArchetype item;

    [ObservableProperty]
    private string title;

    /// <summary>Édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde (voir Save) - même
    /// principe que Item lui-même (une copie, rien n'est persisté avant Enregistrer), contrairement aux
    /// Équipement/Compétences/Blessures d'un Warrior qui persistent immédiatement.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>Options du Picker "Liste d'équipement" - même pattern que SpellEditDialogViewModel's
    /// MagicSchool picker (label -> ligne catalogue, écrit Item.EquipmentListId via
    /// OnSelectedEquipmentListLabelChanged), avec en plus une entrée "Aucune" puisque EquipmentListId
    /// est nullable (beaucoup d'archétypes - Zombies, Trolls... - n'utilisent aucun équipement).</summary>
    public ObservableCollection<string> EquipmentListOptions { get; } = new();

    [ObservableProperty]
    private string selectedEquipmentListLabel = string.Empty;

    /// <summary>Champ texte unique pour le Mouvement - accepte un nombre ("4") ou une surcharge libre
    /// ("2D6" pour les Squigs des cavernes). Résolu vers Item.Movement/Item.MovementOverride au Save
    /// selon que ça parse comme int ou non, plutôt que 2 champs séparés (Entry numérique + Entry
    /// texte) - un seul champ, comme sur la fiche officielle.</summary>
    [ObservableProperty]
    private string movementInput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int currentStep;

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool CanGoBack => CurrentStep > 0;
    public bool IsLastStep => CurrentStep == StepCount - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], CurrentStep + 1, StepCount);

    public WarriorArchetypeEditDialogViewModel(WarriorArchetype item, string title, ISpecialRulePickerService specialRulePicker,
        IReadOnlyList<EquipmentList> allEquipmentLists)
    {
        this.item = item;
        this.title = title;
        _specialRulePicker = specialRulePicker;
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
        movementInput = item.MovementOverride ?? item.Movement.ToString();

        var noneLabel = Loc["WarriorArchetypeEquipmentListNone"];
        EquipmentListOptions.Add(noneLabel);
        foreach (var list in allEquipmentLists)
        {
            _equipmentListByLabel[list.Name] = list;
            EquipmentListOptions.Add(list.Name);
        }
        var currentList = allEquipmentLists.FirstOrDefault(l => l.Id == item.EquipmentListId);
        selectedEquipmentListLabel = currentList?.Name ?? noneLabel;
    }

    partial void OnSelectedEquipmentListLabelChanged(string value) =>
        Item.EquipmentListId = _equipmentListByLabel.TryGetValue(value, out var list) ? list.Id : null;

    [RelayCommand]
    private async Task AddSpecialRule()
    {
        var picked = await _specialRulePicker.PickSpecialRulesAsync(SpecialRuleScope.Warrior);
        foreach (var rule in picked)
        {
            if (SpecialRules.Any(r => r.Id == rule.Id)) continue;
            SpecialRules.Add(rule);
        }
    }

    [RelayCommand]
    private void RemoveSpecialRule(SpecialRule rule) => SpecialRules.Remove(rule);

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep < StepCount - 1) CurrentStep++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 0) CurrentStep--;
    }

    [RelayCommand]
    private void Save()
    {
        Item.SpecialRules = SpecialRules.ToList();

        if (int.TryParse(MovementInput, out var movement))
        {
            Item.Movement = movement;
            Item.MovementOverride = null;
        }
        else
        {
            Item.MovementOverride = MovementInput;
        }

        Close(true);
    }
}
