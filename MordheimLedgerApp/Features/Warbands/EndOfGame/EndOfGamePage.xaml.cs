using System.ComponentModel;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

public partial class EndOfGamePage : ContentPage
{
    public EndOfGamePage(EndOfGamePageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // Chaque étape repart du haut : la vue change, mais le défilement de la page restait à la position de
    // l'étape précédente.
    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EndOfGamePageViewModel.StepIndex))
            await FormLayout.ScrollToTopAsync();
    }

    /// <summary>Geste retour OS/matériel (ex. Android) - route vers la même commande que le ✕ de l'en-tête
    /// plutôt que de laisser le framework naviguer tout seul, pour appliquer la même confirmation
    /// conditionnelle (EndOfGamePageViewModel.Exit).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is EndOfGamePageViewModel vm)
            vm.ExitCommand.Execute(null);
        return true;
    }
}
