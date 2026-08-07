using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

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

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "CO"/"GC" en toutes
    /// lettres, même formule que EquipmentItemRow.CostDisplay.</summary>
    public string CostDisplay
    {
        get
        {
            var abbr = LocalizationService.Instance["LibGoldCrownsAbbr"];
            return Item.Rarity.HasValue ? $"{Item.Cost} {abbr} · R{Item.Rarity}" : $"{Item.Cost} {abbr}";
        }
    }

    public MountRow(Mount item) => Item = item;
}
