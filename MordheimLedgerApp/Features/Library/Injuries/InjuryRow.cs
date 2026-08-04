using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Injuries;

/// <summary>
/// Tuile de grille (InjuryView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// EquipmentItemRow/SkillRow.
/// </summary>
public partial class InjuryRow : ObservableObject
{
    public Injury Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public InjuryRow(Injury item) => Item = item;
}
