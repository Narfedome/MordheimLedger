using System.ComponentModel;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>
/// Wrapper XAML lié à WarbandEditDialogViewModel, où vit toute la logique du dialog. Seul ajout ici : les
/// visites guidées de l'assistant de création (cf. AppTourService), une par étape - Général à
/// l'ouverture, Guerriers et Équipement à la première arrivée sur chacune - plutôt qu'un seul long
/// parcours qui devrait faire avancer l'assistant lui-même.
/// </summary>
public partial class WarbandEditDialog : DialogContent<bool>
{
    private readonly WarbandEditDialogViewModel _viewModel;

    public WarbandEditDialog(WarbandEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Création pas à pas seulement : la réouverture d'une bande existante (onglets libres) n'a pas
        // de visite.
        if (!viewModel.IsWizardMode) return;

        Loaded += OnLoaded;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private static AppTourService? TourService => IPlatformApplication.Current?.Services.GetService<AppTourService>();

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (TourService is { IsWizardGeneralTourPending: true } tourService)
            await RunTourAsync(tourService.StartWizardGeneralTourAsync);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (TourService is not { } tourService) return;

        if (e.PropertyName == nameof(WarbandEditDialogViewModel.IsWarriorsTab) && _viewModel.IsWarriorsTab
            && tourService.IsWizardWarriorsTourPending)
            await RunTourAsync(tourService.StartWizardWarriorsTourAsync);
        else if (e.PropertyName == nameof(WarbandEditDialogViewModel.IsEquipmentTab) && _viewModel.IsEquipmentTab
                 && tourService.IsWizardEquipmentTourPending)
            await RunTourAsync(tourService.StartWizardEquipmentTourAsync);
    }

    // Laisse l'étape finir de charger ses recrues (Loading.IsLoading) avant de mettre ses contrôles en
    // lumière ; plafonné pour qu'un chargement bloqué ne retienne pas la visite indéfiniment.
    private async Task RunTourAsync(Func<Task> startTour)
    {
        try
        {
            for (var i = 0; i < 50 && _viewModel.Loading.IsLoading; i++)
                await Task.Delay(100);
            await startTour();
        }
        catch (Exception ex)
        {
            // Une visite qui échoue ne doit jamais bloquer l'appli : on la perd, sans plus.
            System.Diagnostics.Debug.WriteLine($"[Tour] {ex}");
        }
    }
}
