using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

public partial class WarbandArchetypeEditDialogViewModel : DialogViewModel<bool>
{
    private readonly ISpecialRulePickerService _specialRulePicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarbandArchetype item;

    [ObservableProperty]
    private string title;

    /// <summary>Édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde (voir Save) - même
    /// principe que WarriorArchetypeEditDialogViewModel.SpecialRules.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    public WarbandArchetypeEditDialogViewModel(WarbandArchetype item, string title, ISpecialRulePickerService specialRulePicker)
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
    private void Save()
    {
        Item.SpecialRules = SpecialRules.ToList();
        Close(true);
    }
}
