using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Races;

/// <summary>
/// Ligne de liste (RaceView) - IsSelected est portée par la ligne elle-même (SelectionMode="None" sur
/// le CollectionView), pas la sélection native - même mécanisme que MagicSchoolRow.
/// </summary>
public partial class RaceRow : ObservableObject
{
    public Race Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public RaceRow(Race item) => Item = item;
}
