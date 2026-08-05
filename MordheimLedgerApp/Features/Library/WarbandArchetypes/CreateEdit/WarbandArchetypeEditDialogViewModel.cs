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
    private readonly IMagicSchoolPickerService _magicSchoolPicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarbandArchetype item;

    [ObservableProperty]
    private string title;

    /// <summary>Édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde (voir Save) - même
    /// principe que WarriorArchetypeEditDialogViewModel.SpecialRules.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>Édité en mémoire ici, recopié sur Item.MagicSchools à la sauvegarde - même principe que
    /// SpecialRules. Détermine quels sorts sont proposés aux casters de cette bande (voir
    /// WarriorEditDialogViewModel.AddSpell).</summary>
    public ObservableCollection<MagicSchool> MagicSchools { get; }

    public WarbandArchetypeEditDialogViewModel(WarbandArchetype item, string title, ISpecialRulePickerService specialRulePicker,
        IMagicSchoolPickerService magicSchoolPicker)
    {
        this.item = item;
        this.title = title;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
        MagicSchools = new ObservableCollection<MagicSchool>(item.MagicSchools);
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
    private async Task AddMagicSchool()
    {
        var picked = await _magicSchoolPicker.PickMagicSchoolsAsync();
        foreach (var school in picked)
        {
            if (MagicSchools.Any(s => s.Id == school.Id)) continue;
            MagicSchools.Add(school);
        }
    }

    [RelayCommand]
    private void RemoveMagicSchool(MagicSchool school) => MagicSchools.Remove(school);

    [RelayCommand]
    private void Save()
    {
        Item.SpecialRules = SpecialRules.ToList();
        Item.MagicSchools = MagicSchools.ToList();
        Close(true);
    }
}
