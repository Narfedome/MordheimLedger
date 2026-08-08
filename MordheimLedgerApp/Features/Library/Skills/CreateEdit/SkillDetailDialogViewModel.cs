using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

/// <summary>Read-only recap of SkillEditDialog.</summary>
public partial class SkillDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Skill Item { get; }
    public string CategoryLabel { get; }

    /// <summary>Already resolved by the caller (SkillViewModel.ShowDetails), same fetch-then-filter
    /// idiom as SkillViewModel.Edit's initialWarriors.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }
    public List<WarriorArchetype> RestrictedWarriors { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice affichés en même temps liste vide -
    /// même principe que SkillEditDialogViewModel.RestrictedWarbandsHeaderText.</summary>
    public string RestrictedWarbandsHeaderText =>
        RestrictedWarbands.Count > 0 ? Loc["LibRestrictedToWarbandsPh"] : Loc["LibRestrictedToAllHint"];

    public SkillDetailDialogViewModel(Skill item, string categoryLabel,
        List<WarbandArchetype> restrictedWarbands, List<WarriorArchetype> restrictedWarriors)
    {
        Item = item;
        Title = item.Name;
        CategoryLabel = categoryLabel;
        RestrictedWarbands = restrictedWarbands;
        RestrictedWarriors = restrictedWarriors;
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowWarriorDetail(WarriorArchetype warrior) => ShowChipDetailAsync(warrior.Name, warrior.Description);
}
