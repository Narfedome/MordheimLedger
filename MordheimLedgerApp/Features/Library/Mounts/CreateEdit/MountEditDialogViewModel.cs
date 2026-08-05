using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Mounts.CreateEdit;

/// <summary>Same wizard pattern (identité / profil / restrictions) as WarriorArchetypeEditDialogViewModel
/// - restriction/special-rules chips edited in memory here, recopiés sur Item à Save (voir Save),
/// comme SpecialRules sur WarriorArchetypeEditDialogViewModel.</summary>
public partial class MountEditDialogViewModel : DialogViewModel<bool>
{
    private const int StepCount = 3;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Mount item;

    [ObservableProperty]
    private string title;

    /// <summary>Vide = commun à toutes les bandes (voir Mount.RestrictedToWarbandArchetypeIds).</summary>
    public ObservableCollection<WarbandArchetype> RestrictedWarbands { get; }

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

    public MountEditDialogViewModel(Mount item, string title, IWarbandArchetypePickerService warbandPicker,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes, ISpecialRulePickerService specialRulePicker)
    {
        this.item = item;
        this.title = title;
        _warbandPicker = warbandPicker;
        _specialRulePicker = specialRulePicker;

        RestrictedWarbands = new ObservableCollection<WarbandArchetype>(
            allWarbandArchetypes.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)));
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
    }

    [RelayCommand]
    private async Task AddRestriction()
    {
        var picked = await _warbandPicker.PickWarbandArchetypesAsync();
        foreach (var warband in picked)
        {
            if (RestrictedWarbands.Any(w => w.Id == warband.Id)) continue;
            RestrictedWarbands.Add(warband);
        }
    }

    [RelayCommand]
    private void RemoveRestriction(WarbandArchetype warband) => RestrictedWarbands.Remove(warband);

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
        Item.RestrictedToWarbandArchetypeIds = RestrictedWarbands.Select(w => w.Id).ToList();
        Item.SpecialRules = SpecialRules.ToList();
        Close(true);
    }
}
