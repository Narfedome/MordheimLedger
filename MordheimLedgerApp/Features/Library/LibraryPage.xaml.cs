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
        await vm.InitializeAsync();

        // Visite de cet écran, la première fois qu'on y arrive (cf. AppTourService) - seulement sur la
        // section Bandes, celle que montrent ses étapes.
        if (_tourService.IsCodexTourPending && vm.IsWarbandsTab)
        {
            try
            {
                await _tourService.StartCodexTourAsync(vm.WarbandArchetypes.WarbandArchetypeGroups.Count > 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tour] {ex}");
            }
        }
    }
}
