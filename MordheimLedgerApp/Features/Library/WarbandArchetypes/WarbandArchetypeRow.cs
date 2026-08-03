using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

/// <summary>
/// Tuile de grille (WarbandArchetypeView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - cf.
/// SelectableGridItemBorderStyle pour la raison (teinte colorAccent Android non évitable via
/// VisualStateManager seul).
/// </summary>
public partial class WarbandArchetypeRow : ObservableObject
{
    public WarbandArchetype Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public WarbandArchetypeRow(WarbandArchetype item) => Item = item;
}
