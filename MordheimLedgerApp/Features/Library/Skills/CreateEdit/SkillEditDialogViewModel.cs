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
    private readonly IWarriorArchetypePickerService _warriorPicker;

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

    /// <summary>Même principe que RestrictedWarbands, un niveau plus bas - vide = tout guerrier des
    /// bandes ci-dessus peut piocher la compétence (voir Skill.RestrictedToWarriorArchetypeIds). Le
    /// picker (AddRestrictedWarriorCommand) se limite aux guerriers de RestrictedWarbands ; retirer une
    /// bande retire aussi ses guerriers déjà cochés (voir RemoveRestriction).</summary>
    public ObservableCollection<WarriorArchetype> RestrictedWarriors { get; }

    /// <summary>Pilote l'affichage du bloc "Réservé à ces guerriers" - inutile tant qu'aucune bande
    /// n'est sélectionnée (rien à restreindre).</summary>
    public bool HasRestrictedWarbands => RestrictedWarbands.Count > 0;

    public SkillEditDialogViewModel(Skill item, string title, IWarbandArchetypePickerService warbandPicker,
        IWarriorArchetypePickerService warriorPicker, IReadOnlyList<WarbandArchetype> allWarbandArchetypes,
        IReadOnlyList<WarriorArchetype> initialRestrictedWarriors)
    {
        this.item = item;
        this.title = title;
        _warbandPicker = warbandPicker;
        _warriorPicker = warriorPicker;

        foreach (var category in Enum.GetValues<SkillCategory>())
        {
            var label = Loc[$"SkillCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"SkillCategory{item.Category}"];
        RestrictedWarbands = new ObservableCollection<WarbandArchetype>(
            allWarbandArchetypes.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)));
        RestrictedWarriors = new ObservableCollection<WarriorArchetype>(initialRestrictedWarriors);
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
        OnPropertyChanged(nameof(HasRestrictedWarbands));
    }

    [RelayCommand]
    private void RemoveRestriction(WarbandArchetype warband)
    {
        RestrictedWarbands.Remove(warband);

        // Une restriction guerrier ne peut pas survivre à sa restriction bande.
        foreach (var warrior in RestrictedWarriors.Where(w => w.WarbandArchetypeId == warband.Id).ToList())
            RestrictedWarriors.Remove(warrior);
        OnPropertyChanged(nameof(HasRestrictedWarbands));
    }

    [RelayCommand]
    private async Task AddRestrictedWarrior()
    {
        var picked = await _warriorPicker.PickWarriorArchetypesAsync(RestrictedWarbands.Select(w => w.Id));
        foreach (var warrior in picked)
        {
            if (RestrictedWarriors.Any(w => w.Id == warrior.Id)) continue;
            RestrictedWarriors.Add(warrior);
        }
    }

    [RelayCommand]
    private void RemoveRestrictedWarrior(WarriorArchetype warrior) => RestrictedWarriors.Remove(warrior);

    [RelayCommand]
    private void Save()
    {
        Item.RestrictedToWarbandArchetypeIds = RestrictedWarbands.Select(w => w.Id).ToList();
        Item.RestrictedToWarriorArchetypeIds = RestrictedWarriors.Select(w => w.Id).ToList();
        Close(true);
    }
}
