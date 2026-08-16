using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

/// <summary>Read-only recap of SkillEditDialog.</summary>
public partial class SkillDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Skill Item { get; }
    public string CategoryLabel { get; }

    /// <summary>Already resolved by the caller (SkillViewModel.ShowDetails), same fetch-then-filter
    /// idiom as SkillViewModel.Edit's initialWarriors. Collapsed to its complement against
    /// allWarbandArchetypes when it covers more than half the catalog - see WarbandRestrictionDisplay.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }
    public List<WarriorArchetype> RestrictedWarriors { get; }

    /// <summary>Reflects whichever of Include/Exclude RestrictedWarbands ended up collapsed to - see
    /// WarbandRestrictionDisplay.HeaderTextFor.</summary>
    public string RestrictedWarbandsHeaderText { get; }

    public SkillDetailDialogViewModel(Skill item, string categoryLabel,
        List<WarbandArchetype> restrictedWarbands, List<WarbandArchetype> allWarbandArchetypes, List<WarriorArchetype> restrictedWarriors)
    {
        Item = item;
        Title = item.Name;
        CategoryLabel = categoryLabel;
        RestrictedWarbands = WarbandRestrictionDisplay.DisplayedFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarbandsHeaderText = WarbandRestrictionDisplay.HeaderTextFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarriors = restrictedWarriors;
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowWarriorDetail(WarriorArchetype warrior) => ShowChipDetailAsync(warrior.Name, warrior.Description);
}
