using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.EquipmentLists;

/// <summary>
/// Tuile de grille (EquipmentListView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - cf.
/// SelectableGridItemBorderStyle/WarriorArchetypeRow.
/// </summary>
public partial class EquipmentListRow : ObservableObject
{
    public EquipmentList Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public EquipmentListRow(EquipmentList item) => Item = item;
}
