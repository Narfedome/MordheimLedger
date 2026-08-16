using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, SkillCategory> _categoryByLabel = new();
    private readonly IWarriorArchetypePickerService _warriorPicker;

    protected override bool CancelResult => false;

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private Skill item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même Include/Exclude editor qu'EquipmentItemEditDialogViewModel.WarbandRestriction. Vide = commun
    /// à toutes les bandes (voir Skill.RestrictedToWarbandArchetypeIds). Changed prune RestrictedWarriors
    /// dès qu'une bande sort de l'ensemble inclus, quel que soit le mode utilisé pour l'en sortir.</summary>
    public WarbandRestrictionEditor WarbandRestriction { get; }

    /// <summary>Même principe que WarbandRestriction, un niveau plus bas - vide = tout guerrier des
    /// bandes restreintes peut piocher la compétence (voir Skill.RestrictedToWarriorArchetypeIds). Le
    /// picker (AddRestrictedWarriorCommand) se limite aux guerriers de WarbandRestriction.SelectedIds
    /// (l'ensemble inclus réel, pas les chips affichés qui peuvent être les bandes exclues en mode
    /// Exclure) ; retirer une bande de l'ensemble inclus retire aussi ses guerriers déjà cochés (voir
    /// OnWarbandRestrictionChanged).</summary>
    public ObservableCollection<WarriorArchetype> RestrictedWarriors { get; }

    /// <summary>Pilote l'affichage du bloc "Réservé à ces guerriers" - inutile tant qu'aucune bande
    /// n'est incluse (rien à restreindre).</summary>
    public bool HasRestrictedWarbands => WarbandRestriction.SelectedIds.Count > 0;

    /// <summary>Même principe un niveau en dessous - "Réservé à ces guerriers" tant qu'il y a des
    /// guerriers cochés, remplacé par l'indice "vide = tout guerrier des bandes sélectionnées" sinon.</summary>
    public string RestrictedWarriorsHeaderText =>
        RestrictedWarriors.Count > 0 ? Loc["LibRestrictedToWarriorsPh"] : Loc["LibRestrictedToWarriorsHint"];

    public SkillEditDialogViewModel(Skill item, string title, IWarbandArchetypePickerService warbandPicker,
        IWarriorArchetypePickerService warriorPicker, IReadOnlyList<WarbandArchetype> allWarbandArchetypes,
        IReadOnlyList<WarriorArchetype> initialRestrictedWarriors)
    {
        this.item = item;
        this.title = title;
        _warriorPicker = warriorPicker;

        foreach (var category in Enum.GetValues<SkillCategory>())
        {
            var label = Loc[$"SkillCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"SkillCategory{item.Category}"];
        WarbandRestriction = new WarbandRestrictionEditor(item.RestrictedToWarbandArchetypeIds, allWarbandArchetypes, warbandPicker);
        WarbandRestriction.Changed += OnWarbandRestrictionChanged;
        RestrictedWarriors = new ObservableCollection<WarriorArchetype>(initialRestrictedWarriors);
    }

    partial void OnSelectedCategoryLabelChanged(string value)
    {
        if (_categoryByLabel.TryGetValue(value, out var category))
            Item.Category = category;
    }

    /// <summary>Une restriction guerrier ne peut pas survivre à sa restriction bande - prune dès que
    /// l'ensemble des bandes réellement incluses change, peu importe si le changement venait du mode
    /// Inclure ou Exclure.</summary>
    private void OnWarbandRestrictionChanged()
    {
        var includedIds = WarbandRestriction.SelectedIds;
        foreach (var warrior in RestrictedWarriors.Where(w => !includedIds.Contains(w.WarbandArchetypeId)).ToList())
            RestrictedWarriors.Remove(warrior);
        OnPropertyChanged(nameof(HasRestrictedWarbands));
        OnPropertyChanged(nameof(RestrictedWarriorsHeaderText));
    }

    [RelayCommand]
    private async Task AddRestrictedWarrior()
    {
        var picked = await _warriorPicker.PickWarriorArchetypesAsync(WarbandRestriction.SelectedIds);
        foreach (var warrior in picked)
        {
            if (RestrictedWarriors.Any(w => w.Id == warrior.Id)) continue;
            RestrictedWarriors.Add(warrior);
        }
        OnPropertyChanged(nameof(RestrictedWarriorsHeaderText));
    }

    [RelayCommand]
    private void RemoveRestrictedWarrior(WarriorArchetype warrior)
    {
        RestrictedWarriors.Remove(warrior);
        OnPropertyChanged(nameof(RestrictedWarriorsHeaderText));
    }

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
        if (!ValidateRequiredFields()) return;

        Item.RestrictedToWarbandArchetypeIds = WarbandRestriction.SelectedIds;
        Item.RestrictedToWarriorArchetypeIds = RestrictedWarriors.Select(w => w.Id).ToList();
        Close(true);
    }
}
