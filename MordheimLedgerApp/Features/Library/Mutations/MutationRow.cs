using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Mutations;

/// <summary>
/// Tuile de grille (MutationView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// InjuryRow/SpecialRuleRow.
/// </summary>
public partial class MutationRow : ObservableObject
{
    public Mutation Item { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "CO"/"GC" en toutes
    /// lettres, même formule que EquipmentItemRow.CostDisplay.</summary>
    public string CostDisplay => $"{Item.Cost} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";

    public MutationRow(Mutation item) => Item = item;
}
