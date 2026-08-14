using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>
/// Ligne de la liste des bandes : porte l'état de sélection en plus du modèle, la sélection n'étant
/// pas gérée par le CollectionView natif (son rendu diverge trop entre Windows et Android) - cf.
/// SelectionMarkerStyle dans Styles.xaml.
/// </summary>
public partial class WarbandRow : ObservableObject
{
    public Warband Warband { get; }
    public string Name => Warband.Name;

    [ObservableProperty]
    private string archetypeName = string.Empty;

    /// <summary>Rulebook "calculate the warband rating" - see IWarbandService.GetWarbandRatingAsync.
    /// Fetched alongside ArchetypeName at list-load time.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingDisplay))]
    private int rating;

    public string RatingDisplay => string.Format(LocalizationService.Instance["WarbandRatingDisplay"], Rating);

    [ObservableProperty]
    private bool isSelected;

    public WarbandRow(Warband warband) => Warband = warband;
}
