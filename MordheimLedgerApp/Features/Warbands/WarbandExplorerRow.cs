using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

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
    private bool isSelected;

    public WarbandRow(Warband warband) => Warband = warband;
}
