namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandDetailPage : ContentPage
{
    public WarbandDetailPage(WarbandDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Cf. SettingsPage.xaml.cs / CategoryListPage de DmTools : plafonne/centre sur Desktop
        // uniquement, en code plutôt qu'en XAML (MaximumWidthRequest n'a pas de valeur sentinelle
        // "pas de contrainte" sûre à toucher sur Android/iOS). WidthRequest (pas juste Maximum) sur
        // les deux éléments : avec HorizontalOptions=Center, un élément se centre à sa taille
        // naturelle, pas à sa largeur maximale - Header (juste une flèche + un titre) se réduirait à
        // son texte et se centrerait décalé par rapport à RosterScroll.
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop)
        {
            Header.WidthRequest = 560;
            Header.HorizontalOptions = LayoutOptions.Center;
            RosterScroll.WidthRequest = 560;
            RosterScroll.HorizontalOptions = LayoutOptions.Center;
        }
    }

    // Rafraîchit à chaque retour sur cette page (ex. depuis EndOfGamePage, qui persiste puis navigue
    // en arrière) - même pattern que les pages Bibliothèque (ex. RaceListPage.xaml.cs). Nécessaire
    // depuis que Fin de Partie est une page Shell distincte plutôt qu'une dialog : OnWarbandIdChanged
    // (WarbandDetailViewModel) ne se redéclenche pas quand l'id est inchangé après un
    // Shell.GoToAsync(".."), contrairement à l'ancien flux où EndOfGame() rappelait LoadAsync
    // directement dans le même appel de commande.
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is WarbandDetailViewModel vm)
            await vm.LoadAsync(vm.WarbandId);
    }
}
