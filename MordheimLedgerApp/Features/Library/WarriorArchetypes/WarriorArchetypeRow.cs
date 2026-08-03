using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes;

/// <summary>
/// Tuile de grille (WarriorArchetypeView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - cf.
/// SelectableGridItemBorderStyle pour la raison (teinte colorAccent Android non évitable via
/// VisualStateManager seul).
/// </summary>
public partial class WarriorArchetypeRow : ObservableObject
{
    public WarriorArchetype Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public WarriorArchetypeRow(WarriorArchetype item) => Item = item;
}
