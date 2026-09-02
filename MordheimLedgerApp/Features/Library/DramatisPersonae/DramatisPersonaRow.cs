using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae;

/// <summary>Tuile de grille (DramatisPersonaView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), même mécanisme que HiredSwordRow.</summary>
public partial class DramatisPersonaRow : ObservableObject
{
    public DramatisPersona Item { get; }

    /// <summary>What the tile's name label actually binds to - Item.Name by default, but overridden by
    /// DramatisPersonaViewModel.LoadData to the combined "Marquand Volker &amp; Ulli Leitpold" in the
    /// "Personnage spécial" recruitment picker specifically (2026-09-01, user request: the picker's single
    /// combined tile - Ulli hidden there, see DramatisPersona.IsHiddenFromSearchPicker - must read as the
    /// pair, unlike the normal Codex grid where both keep their own separate tile/name). Item.Name stays
    /// untouched either way - this is purely a tile display concern.</summary>
    public string DisplayName { get; }

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

    public DramatisPersonaRow(DramatisPersona item, string? displayName = null)
    {
        Item = item;
        DisplayName = displayName ?? item.Name;
    }
}
