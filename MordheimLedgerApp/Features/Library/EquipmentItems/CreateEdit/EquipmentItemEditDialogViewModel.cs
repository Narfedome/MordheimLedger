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

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même principe que WarriorArchetypeEditDialogViewModel.SpecialRules. Vide = commun à toutes les
    /// bandes (voir EquipmentItem.RestrictedToWarbandArchetypeIds).</summary>
    public ObservableCollection<WarbandArchetype> RestrictedWarbands { get; }

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
    private void Save()
    {
        Item.RestrictedToWarbandArchetypeIds = RestrictedWarbands.Select(w => w.Id).ToList();
        Item.SpecialRules = SpecialRules.ToList();
        Close(true);
    }
}
