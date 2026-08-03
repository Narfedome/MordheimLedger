using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

/// <summary>
/// Tuile de grille (EquipmentItemView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - cf.
/// SelectableGridItemBorderStyle pour la raison (teinte colorAccent Android non évitable via
/// VisualStateManager seul).
/// </summary>
public partial class EquipmentItemRow : ObservableObject
{
    public EquipmentItem Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public EquipmentItemRow(EquipmentItem item) => Item = item;
}
