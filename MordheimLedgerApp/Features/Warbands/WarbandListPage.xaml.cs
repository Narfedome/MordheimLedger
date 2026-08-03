namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListPage : ContentPage
{
    public WarbandListPage(WarbandListViewModel viewModel)
    {
        InitializeComponent();
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is WarbandListViewModel vm)
            vm.LoadWarbandsCommand.Execute(null);
    }
}
