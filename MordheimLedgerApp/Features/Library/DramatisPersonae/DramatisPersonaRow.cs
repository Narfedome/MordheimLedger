using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae;

/// <summary>Tuile de grille (DramatisPersonaView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), même mécanisme que HiredSwordRow.</summary>
public partial class DramatisPersonaRow : ObservableObject
{
    public DramatisPersona Item { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - le libellé varie
    /// selon FeeKind (voir DramatisPersonaHireFeeKind) : les 4 formes de frais du livre ne se résument
    /// pas toutes à "coût + entretien" comme HiredSwordRow.HireCostDisplay.</summary>
    public string FeeDisplay
    {
        get
        {
            var loc = LocalizationService.Instance;
            return Item.FeeKind switch
            {
                DramatisPersonaHireFeeKind.None => loc["DramatisPersonaFeeNone"],
                DramatisPersonaHireFeeKind.Wyrdstone => loc["DramatisPersonaFeeWyrdstone"],
                DramatisPersonaHireFeeKind.Pair => string.Format(loc["DramatisPersonaFeePairFormat"], Item.HireCost),
                _ => Item.Upkeep is { } upkeep
                    ? $"{Item.HireCost} {loc["LibGoldCrownsAbbr"]} (+{upkeep})"
                    : $"{Item.HireCost} {loc["LibGoldCrownsAbbr"]}"
            };
        }
    }

    public DramatisPersonaRow(DramatisPersona item) => Item = item;
}
