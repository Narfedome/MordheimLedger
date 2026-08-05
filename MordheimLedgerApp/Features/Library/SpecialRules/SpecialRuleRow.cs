using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.SpecialRules;

/// <summary>
/// Tuile de grille (SpecialRuleView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// InjuryRow/SkillRow/EquipmentItemRow.
/// </summary>
public partial class SpecialRuleRow : ObservableObject
{
    public SpecialRule Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public SpecialRuleRow(SpecialRule item) => Item = item;
}
