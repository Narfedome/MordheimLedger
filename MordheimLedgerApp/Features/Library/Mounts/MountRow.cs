using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mounts;

/// <summary>
/// Tuile de grille (MountView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// InjuryRow/MutationRow.
/// </summary>
public partial class MountRow : ObservableObject
{
    public Mount Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public MountRow(Mount item) => Item = item;
}
