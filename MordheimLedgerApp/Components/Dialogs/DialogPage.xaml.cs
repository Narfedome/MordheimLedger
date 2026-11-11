namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Page modale générique qui héberge le contenu d'UN dialog (voir DialogStack) - remplace
/// l'ancien DialogHostPopup (CommunityToolkit.Maui Popup) : une vraie Page poussée via
/// Navigation.PushModalAsync a une taille de fenêtre non ambiguë et un hit-testing normal, contrairement
/// au Popup du toolkit qui s'auto-dimensionnait mal une fois réutilisé comme conteneur générique.</summary>
public partial class DialogPage : ContentPage
{
    private readonly Action _onBackdropTapped;

    public DialogPage(View content, Action onBackdropTapped)
    {
        InitializeComponent();
        _onBackdropTapped = onBackdropTapped;

        // Aucun des 27 dialogs ne fixe son propre HorizontalOptions (seule la carte/Border a une
        // WidthRequest fixe) - laissé à Fill (défaut hérité), la racine du dialog s'étirait sur toute la
        // largeur de la page. Centré ici : ses bounds se réduisent à la largeur réelle de la carte, pour
        // que le tap "en dehors" (à gauche/droite d'une carte étroite) retombe bien sur le scrim au lieu
        // d'être avalé par la racine du dialog.
        content.HorizontalOptions = LayoutOptions.Center;
        // Placement commun à tous les dialogs (refonte de novembre 2026, maquette « Refonte dialogs et Fin de
        // partie ») : centrés verticalement sur toutes les plateformes - avant, collés à 80 px du haut sur
        // Android et centrés sur Windows - et largeur adaptée à l'écran plutôt que 320/340/360 fixes.
        content.VerticalOptions = LayoutOptions.Center;
        // Centré dans la zone au-dessus de la barre d'onglets du Shell (mobile) plutôt que sur tout l'écran :
        // marge basse = hauteur de la barre (DialogSizing.BottomBarInset).
        content.Margin = new Thickness(0, 16, 0, 16 + DialogSizing.BottomBarInset());
        _card = (content as ContentView)?.Content as View;
        // Largeur posée dès la construction (taille de la fenêtre) pour éviter un premier rendu à la largeur
        // naturelle du contenu, puis ajustée à chaque redimensionnement de la page.
        var windowWidth = Application.Current?.Windows.FirstOrDefault()?.Width ?? 0;
        if (_card is not null && windowWidth > 0) _card.WidthRequest = DialogSizing.CardWidth(windowWidth);
        Presenter.Content = content;
        SizeChanged += (_, _) => ApplyCardWidth();

        // Recognizer vide : rend la racine du dialog (content) hit-test opaque sur toute sa surface
        // mesurée (y compris le texte non interactif dedans), pour qu'un tap dessus ne retombe pas sur
        // le BoxView en dessous - centralisé ici plutôt que dupliqué dans les 27 dialogs.
        content.GestureRecognizers.Add(new TapGestureRecognizer());
    }

    private readonly View? _card;

    private void ApplyCardWidth()
    {
        if (_card is null || Width <= 0) return;
        _card.WidthRequest = DialogSizing.CardWidth(Width);
    }

    private void OnBackdropTapped(object? sender, TappedEventArgs e) => _onBackdropTapped();

    /// <summary>Bouton retour Android : même chemin que le tap sur le fond (CancelCommand, donc avec la
    /// confirmation "modifications non enregistrées" si besoin) plutôt que de laisser la plateforme
    /// dépiler la page d'office - une fois dépilée, il serait trop tard pour demander (voir
    /// DialogStack.OnModalPopped).</summary>
    protected override bool OnBackButtonPressed()
    {
        _onBackdropTapped();
        return true;
    }
}
