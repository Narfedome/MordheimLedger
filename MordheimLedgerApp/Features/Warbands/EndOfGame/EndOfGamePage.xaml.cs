namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

public partial class EndOfGamePage : ContentPage
{
    public EndOfGamePage(EndOfGamePageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Geste retour OS/matériel (ex. Android) - une page Shell n'a pas d'équivalent gratuit au
    /// "fermeture modale = Annuler silencieux" de l'ancienne dialog (voir DialogStack.ModalPopped).
    /// Route vers la même commande que la flèche de DetailPageHeaderView plutôt que de laisser le
    /// framework naviguer tout seul, pour appliquer la même confirmation conditionnelle
    /// (EndOfGamePageViewModel.Exit).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is EndOfGamePageViewModel vm)
            vm.ExitCommand.Execute(null);
        return true;
    }
}
