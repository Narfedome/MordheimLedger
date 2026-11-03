using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandDetailPage : ContentPage
{
    private readonly AppTourService _tourService;

    public WarbandDetailPage(WarbandDetailViewModel viewModel, AppTourService tourService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _tourService = tourService;

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

    // Visite de cet écran, la première fois qu'une fiche de bande s'ouvre (cf. AppTourService), une fois
    // le roster chargé par le paramètre de navigation. Remonte d'abord en haut du roster : les étapes sur
    // la carte d'un guerrier visent la première du roster, et le plugin ne fait pas défiler. Visite déjà
    // faite : seule l'étape photo, arrivée après coup, est proposée, dès qu'une carte de héros l'affiche.
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (!_tourService.IsDetailTourPending && !_tourService.IsDetailPhotoTourPending) return;
        if (BindingContext is not WarbandDetailViewModel vm) return;
        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        try
        {
            await vm.InitialLoad;
            if (vm.Warband is null) return;
            var hasHeroes = vm.Heroes.Count > 0;
            if (!_tourService.IsDetailTourPending && !hasHeroes) return;
            await RosterScroll.ScrollToAsync(0, 0, false);
            if (_tourService.IsDetailTourPending)
                await _tourService.StartDetailTourAsync(hasHeroes, vm.HasInventory);
            else
                await _tourService.StartDetailPhotoTourAsync();
        }
        catch (Exception ex)
        {
            // Une visite qui échoue ne doit jamais bloquer l'appli : on la perd, sans plus.
            System.Diagnostics.Debug.WriteLine($"[Tour] {ex}");
        }
    }

    // Rafraîchit à chaque retour sur cette page (ex. depuis EndOfGamePage, qui persiste puis navigue
    // en arrière) - même pattern que les pages Bibliothèque (ex. RaceListPage.xaml.cs). Nécessaire
    // depuis que Fin de Partie est une page Shell distincte plutôt qu'une dialog : OnWarbandIdChanged
    // (WarbandDetailViewModel) ne se redéclenche pas quand l'id est inchangé après un
    // Shell.GoToAsync(".."), contrairement à l'ancien flux où EndOfGame() rappelait LoadAsync
    // directement dans le même appel de commande. Pas au PREMIER affichage : le paramètre de
    // navigation (warbandId) a déjà déclenché LoadAsync via OnWarbandIdChanged - recharger ici aussi
    // construisait tout le roster deux fois à chaque ouverture (repéré le 2026-09-24).
    private bool _hasAppeared;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasAppeared)
        {
            _hasAppeared = true;
            return;
        }

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is WarbandDetailViewModel vm)
            await vm.LoadAsync(vm.WarbandId);
    }
}
