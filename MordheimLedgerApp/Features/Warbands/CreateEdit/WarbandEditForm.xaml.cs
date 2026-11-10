using System.ComponentModel;
using MordheimLedgerApp.Components.FormPage;
using MordheimLedgerApp.Services;
using Plugin.Maui.Onboarding;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>
/// Page plein écran de création/modification d'une bande (autrefois WarbandEditDialog), liée à
/// WarbandEditDialogViewModel où vit toute la logique. Ici : les cibles de visite posées sur l'en-tête et le
/// pied du gabarit commun, le retour en haut du contenu à chaque changement d'étape, et les visites guidées
/// de l'assistant de création (cf. AppTourService), une par étape - Général à l'ouverture, Guerriers et
/// Équipement à la première arrivée sur chacune.
/// </summary>
public partial class WarbandEditForm : FormContent<bool>
{
    private readonly WarbandEditDialogViewModel _viewModel;

    public WarbandEditForm(WarbandEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Création pas à pas seulement : la réouverture d'une bande existante (onglets libres) n'a pas de visite.
        if (!viewModel.IsWizardMode) return;

        FormLayout.StepTextView.Behaviors.Add(new OnboardingTargetBehavior { Key = AppTourService.WizardStepKey });
        FormLayout.FooterView.Behaviors.Add(new OnboardingTargetBehavior { Key = AppTourService.WizardNavKey });
        Loaded += OnLoaded;
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
        if (e.PropertyName == nameof(WarbandEditDialogViewModel.SelectedTab))
            await FormLayout.ScrollToTopAsync();

        if (!_viewModel.IsWizardMode || TourService is not { } tourService) return;

        if (e.PropertyName == nameof(WarbandEditDialogViewModel.IsWarriorsTab) && _viewModel.IsWarriorsTab
            && tourService.IsWizardWarriorsTourPending)
            await RunTourAsync(tourService.StartWizardWarriorsTourAsync);
        else if (e.PropertyName == nameof(WarbandEditDialogViewModel.IsEquipmentTab) && _viewModel.IsEquipmentTab
                 && tourService.IsWizardEquipmentTourPending)
            await RunTourAsync(tourService.StartWizardEquipmentTourAsync);
    }

    // Laisse l'étape finir de charger ses recrues (Loading.IsLoading) puis la mise en page se poser avant de
    // mettre ses contrôles en lumière : mesurées trop tôt, les cibles étaient décalées. Plafonné pour qu'un
    // chargement bloqué ne retienne pas la visite indéfiniment.
    private async Task RunTourAsync(Func<Task> startTour)
    {
        try
        {
            for (var i = 0; i < 50 && _viewModel.Loading.IsLoading; i++)
                await Task.Delay(100);
            await Task.Delay(150);
            await startTour();
        }
        catch (Exception ex)
        {
            // Une visite qui échoue ne doit jamais bloquer l'appli : on la perd, sans plus.
            System.Diagnostics.Debug.WriteLine($"[Tour] {ex}");
        }
    }
}
