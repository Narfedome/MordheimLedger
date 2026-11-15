namespace MordheimLedgerApp.Features.Lobby;

/// <summary>Fiche d'une bande adverse (voir OpponentWarbandViewModel), route « OpponentWarbandPage?playerId=… ».</summary>
public partial class OpponentWarbandPage : ContentPage
{
    public OpponentWarbandPage(OpponentWarbandViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
