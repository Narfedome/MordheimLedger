using CommunityToolkit.Maui.Extensions;
using MordheimLedgerApp.Features.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListPage : ContentPage
{
    private readonly WarbandListViewModel _vm;
    private readonly AppTourService _tourService;

    public WarbandListPage(WarbandListViewModel viewModel, AppTourService tourService)
    {
        InitializeComponent();
        _vm = viewModel;
        _tourService = tourService;
        BindingContext = viewModel;

        // Cf. SettingsPage.xaml.cs / CategoryListPage de DmTools : plafonne/centre la liste sur
        // Desktop uniquement, en code plutôt qu'en XAML (MaximumWidthRequest n'a pas de valeur
        // sentinelle "pas de contrainte" sûre à toucher sur Android/iOS).
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop)
        {
            WarbandCollection.WidthRequest = 560;
            WarbandCollection.HorizontalOptions = LayoutOptions.Center;
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is not WarbandListViewModel vm) return;

        // Premier chargement (démarrage de l'appli, qui attend aussi l'init de la base) : via
        // LoadWarbandsCommand pour afficher le spinner (Loading.IsLoading) - retours suivants sur
        // l'onglet en silencieux, pour ne pas le faire clignoter à chaque changement d'onglet.
        // Seulement si une écriture a eu lieu depuis le dernier chargement : sinon la liste (et la ligne
        // sélectionnée) restent telles quelles au retour (refonte de novembre 2026).
        if (!vm.IsInitialized)
            await Services.UiErrors.RunAsync(() => vm.LoadWarbandsCommand.ExecuteAsync(null), GetType().Name);
        else if (!vm.IsUpToDate)
            await Services.UiErrors.RunAsync(() => vm.InitializeAsync(), GetType().Name);

        // Visite de cet écran, la première fois qu'on y arrive avec au moins une bande (cf.
        // AppTourService) - la bande d'exemple quand elle vient d'être chargée depuis l'accueil.
        // Page quittée pendant le chargement : la visite attend le prochain retour plutôt que de couvrir l'autre écran.
        if (_tourService.IsWarbandsTourPending && vm.HasWarbands && Shell.Current?.CurrentPage == this)
        {
            try
            {
                await _tourService.StartWarbandsTourAsync(this);
            }
            catch (Exception ex)
            {
                // Une visite qui échoue ne doit jamais bloquer l'appli : on la perd, sans plus.
                System.Diagnostics.Debug.WriteLine($"[Tour] {ex}");
            }
        }
    }
}
