using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Animals.CreateEdit;

/// <summary>3 onglets librement navigables (Général/Profil/Règles), même principe que
/// WarbandArchetypeEditDialogViewModel - restriction/special-rules chips edited in memory here,
/// recopiés sur Item à Save (voir Save).</summary>
public partial class AnimalEditDialogViewModel : DialogViewModel<bool>
{
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Animal item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    /// <summary>Vide = commun à toutes les bandes (voir Animal.RestrictedToWarbandArchetypeIds).</summary>
    public ObservableCollection<WarbandArchetype> RestrictedWarbands { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice affichés en même temps liste vide -
    /// même principe que MutationEditDialogViewModel.RestrictedWarbandsHeaderText.</summary>
    public string RestrictedWarbandsHeaderText =>
        RestrictedWarbands.Count > 0 ? Loc["LibRestrictedToWarbandsPh"] : Loc["LibRestrictedToAllHint"];

    public ObservableCollection<SpecialRule> SpecialRules { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsProfileTab))]
    [NotifyPropertyChangedFor(nameof(IsRulesTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsProfileTab => SelectedTab == 1;
    public bool IsRulesTab => SelectedTab == 2;

    public AnimalEditDialogViewModel(Animal item, string title, IWarbandArchetypePickerService warbandPicker,
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
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowProfileTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowRulesTab() => SelectedTab = 2;

    [RelayCommand]
    private async Task AddRestriction()
    {
        var picked = await _warbandPicker.PickWarbandArchetypesAsync();
        foreach (var warband in picked)
        {
            if (RestrictedWarbands.Any(w => w.Id == warband.Id)) continue;
            RestrictedWarbands.Add(warband);
        }
        OnPropertyChanged(nameof(RestrictedWarbandsHeaderText));
    }

    [RelayCommand]
    private void RemoveRestriction(WarbandArchetype warband)
    {
        RestrictedWarbands.Remove(warband);
        OnPropertyChanged(nameof(RestrictedWarbandsHeaderText));
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
        if (!ValidateRequiredFields())
        {
            SelectedTab = 0;
            return;
        }

        Item.RestrictedToWarbandArchetypeIds = RestrictedWarbands.Select(w => w.Id).ToList();
        Item.SpecialRules = SpecialRules.ToList();
        Close(true);
    }
}
