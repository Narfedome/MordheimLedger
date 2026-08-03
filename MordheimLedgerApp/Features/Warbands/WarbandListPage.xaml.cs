using CommunityToolkit.Maui.Extensions;

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

        // Une popup thémée qui se ferme redéclenche la navigation (cf. CampaignPage de DmTools,
        // même garde) - sans ça, chaque ActionSheet/Prompt fermé pendant CreateWarbandAsync
        // rechargeait la liste en fond avant même que le flux de création soit terminé.
        // Create/Edit/Delete rechargent déjà Rows eux-mêmes après coup, donc seul le tout premier
        // affichage a besoin de ce chargement initial.
        if (args.WasPreviousPageACommunityToolkitPopupPage() || _initialized)
            return;

        _initialized = true;
        await _vm.LoadWarbandsCommand.ExecuteAsync(null);
    }
}
