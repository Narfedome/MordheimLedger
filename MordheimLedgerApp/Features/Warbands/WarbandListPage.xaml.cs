using CommunityToolkit.Maui.Extensions;
using MordheimLedgerApp.Features.Library;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListPage : ContentPage
{
    private readonly WarbandListViewModel _vm;
    private bool _initialized;

    public WarbandListPage(WarbandListViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
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
        if (!vm.IsInitialized)
            await vm.LoadWarbandsCommand.ExecuteAsync(null);
        else
            await vm.InitializeAsync();
    }
}
