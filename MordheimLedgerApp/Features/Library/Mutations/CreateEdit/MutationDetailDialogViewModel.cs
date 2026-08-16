using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

/// <summary>Read-only recap of MutationEditDialog.</summary>
public partial class MutationDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Mutation Item { get; }

    /// <summary>Already resolved by the caller (MutationViewModel.ShowDetails) from the ids on Item -
    /// same idiom as MutationViewModel.GroupNameFor, no service call needed here. Collapsed to its
    /// complement against allWarbandArchetypes when it covers more than half the catalog - see
    /// WarbandRestrictionDisplay.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }

    /// <summary>Reflects whichever of Include/Exclude RestrictedWarbands ended up collapsed to - see
    /// WarbandRestrictionDisplay.HeaderTextFor.</summary>
    public string RestrictedWarbandsHeaderText { get; }

    public MutationDetailDialogViewModel(Mutation item, List<WarbandArchetype> restrictedWarbands, List<WarbandArchetype> allWarbandArchetypes)
    {
        Item = item;
        Title = item.Name;
        RestrictedWarbands = WarbandRestrictionDisplay.DisplayedFor(restrictedWarbands, allWarbandArchetypes);
        RestrictedWarbandsHeaderText = WarbandRestrictionDisplay.HeaderTextFor(restrictedWarbands, allWarbandArchetypes);
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);
}
