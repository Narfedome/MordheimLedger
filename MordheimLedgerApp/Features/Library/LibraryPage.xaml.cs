using CommunityToolkit.Maui.Extensions;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library;

public partial class LibraryPage : ContentPage
{
    private readonly AppTourService _tourService;

    public LibraryPage(LibraryViewModel viewModel, AppTourService tourService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _tourService = tourService;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is not LibraryViewModel vm) return;
        await Services.UiErrors.RunAsync(() => vm.InitializeAsync(), GetType().Name);

        // Visite de cet écran, la première fois qu'on y arrive (cf. AppTourService) - seulement sur la
        // section Bandes, celle que montrent ses étapes.
        // Page quittée pendant le chargement : la visite attend le prochain retour plutôt que de couvrir l'autre écran.
        if (_tourService.IsCodexTourPending && vm.IsWarbandsTab && Shell.Current?.CurrentPage == this)
        {
            try
            {
                await _tourService.StartCodexTourAsync(vm.WarbandArchetypes.WarbandArchetypeGroups.Count > 0, this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tour] {ex}");
            }
        }
    }
}
