using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Animals;

/// <summary>
/// Tuile de grille (AnimalView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// InjuryRow/MutationRow.
/// </summary>
public partial class AnimalRow : ObservableObject
{
    public Animal Item { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "CO"/"GC" en toutes
    /// lettres, même formule que EquipmentItemRow.CostDisplay.</summary>
    public string CostDisplay
    {
        get
        {
            var abbr = LocalizationService.Instance["LibGoldCrownsAbbr"];
            var cost = Item.CostRandomMax is { } max ? $"{Item.Cost}-{Item.Cost + max}" : Item.Cost.ToString();
            return Item.Rarity.HasValue ? $"{cost} {abbr} · R{Item.Rarity}" : $"{cost} {abbr}";
        }
    }

    public AnimalRow(Animal item) => Item = item;
}
