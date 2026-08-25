using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.RacialProfiles;

/// <summary>Ligne de liste (RacialProfileView) - même mécanisme que RaceRow (IsSelected porté par la
/// ligne elle-même, SelectionMode="None" sur le CollectionView).</summary>
public partial class RacialProfileRow : ObservableObject
{
    public RacialProfile Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public RacialProfileRow(RacialProfile item) => Item = item;
}
