using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Skills;

/// <summary>
/// Tuile de grille (SkillView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// EquipmentItemRow.
/// </summary>
public partial class SkillRow : ObservableObject
{
    public Skill Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public SkillRow(Skill item) => Item = item;
}
