using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.HiredSwords;

/// <summary>Tuile de grille (HiredSwordView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), même mécanisme que MutationRow.</summary>
public partial class HiredSwordRow : ObservableObject
{
    public HiredSword Item { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - coût d'engagement +
    /// entretien, même formule "en toutes lettres" que MutationRow.CostDisplay/EquipmentItemRow.</summary>
    public string HireCostDisplay =>
        $"{Item.HireCost} {LocalizationService.Instance["LibGoldCrownsAbbr"]} (+{Item.Upkeep})";

    public HiredSwordRow(HiredSword item) => Item = item;
}
