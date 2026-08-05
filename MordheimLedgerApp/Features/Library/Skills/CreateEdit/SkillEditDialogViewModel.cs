using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, SkillCategory> _categoryByLabel = new();
    private readonly IWarbandArchetypePickerService _warbandPicker;

    protected override bool CancelResult => false;

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private Skill item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même principe qu'EquipmentItemEditDialogViewModel.RestrictedWarbands. Vide = commun à toutes les
    /// bandes (voir Skill.RestrictedToWarbandArchetypeIds).</summary>
    public ObservableCollection<WarbandArchetype> RestrictedWarbands { get; }

    public SkillEditDialogViewModel(Skill item, string title, IWarbandArchetypePickerService warbandPicker,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes)
    {
        this.item = item;
        this.title = title;
        _warbandPicker = warbandPicker;

        foreach (var category in Enum.GetValues<SkillCategory>())
        {
            var label = Loc[$"SkillCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"SkillCategory{item.Category}"];
        RestrictedWarbands = new ObservableCollection<WarbandArchetype>(
            allWarbandArchetypes.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)));
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
    private void Save()
    {
        Item.RestrictedToWarbandArchetypeIds = RestrictedWarbands.Select(w => w.Id).ToList();
        Close(true);
    }
}
