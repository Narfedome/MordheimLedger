using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

public partial class EquipmentItemEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, EquipmentCategory> _categoryByLabel = new();
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;

    protected override bool CancelResult => false;

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private EquipmentItem item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsRulesTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsRulesTab => SelectedTab == 1;

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même principe que WarriorArchetypeEditDialogViewModel.SpecialRules. Vide = commun à toutes les
    /// bandes (voir EquipmentItem.RestrictedToWarbandArchetypeIds).</summary>
    public ObservableCollection<WarbandArchetype> RestrictedWarbands { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice affichés en même temps liste vide -
    /// même principe que MutationEditDialogViewModel.RestrictedWarbandsHeaderText.</summary>
    public string RestrictedWarbandsHeaderText =>
        RestrictedWarbands.Count > 0 ? Loc["LibRestrictedToWarbandsPh"] : Loc["LibRestrictedToAllHint"];

    public ObservableCollection<SpecialRule> SpecialRules { get; }

    public EquipmentItemEditDialogViewModel(EquipmentItem item, string title, IWarbandArchetypePickerService warbandPicker,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes, ISpecialRulePickerService specialRulePicker)
    {
        this.item = item;
        this.title = title;
        _warbandPicker = warbandPicker;
        _specialRulePicker = specialRulePicker;

        foreach (var category in Enum.GetValues<EquipmentCategory>())
        {
            var label = Loc[$"EquipmentCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"EquipmentCategory{item.Category}"];
        RestrictedWarbands = new ObservableCollection<WarbandArchetype>(
            allWarbandArchetypes.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)));
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
    }

    partial void OnSelectedCategoryLabelChanged(string value)
    {
        if (_categoryByLabel.TryGetValue(value, out var category))
            Item.Category = category;
    }

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowRulesTab() => SelectedTab = 1;

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
