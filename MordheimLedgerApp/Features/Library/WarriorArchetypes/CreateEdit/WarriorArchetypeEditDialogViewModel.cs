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

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarriorArchetype item;

    [ObservableProperty]
    private string title;

    /// <summary>Édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde (voir Save) - même
    /// principe que Item lui-même (une copie, rien n'est persisté avant Enregistrer), contrairement aux
    /// Équipement/Compétences/Blessures d'un Warrior qui persistent immédiatement.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

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

    public WarriorArchetypeEditDialogViewModel(WarriorArchetype item, string title, ISpecialRulePickerService specialRulePicker)
    {
        this.item = item;
        this.title = title;
        _specialRulePicker = specialRulePicker;
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
    }

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
        // Normalise "" en null : IsSpellcaster (voir WarbandDetailViewModel.EditWarrior) teste "non
        // nul", un champ vidé par l'utilisateur doit redevenir "pas de lanceur de sorts" plutôt qu'une
        // école de magie vide qui ne matcherait jamais aucun Spell.SpellListName.
        Item.SpellListName = string.IsNullOrWhiteSpace(Item.SpellListName) ? null : Item.SpellListName;
        Close(true);
    }
}
