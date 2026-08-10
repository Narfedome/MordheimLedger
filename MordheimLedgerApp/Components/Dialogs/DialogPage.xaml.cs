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
        Presenter.Content = content;

        // Recognizer vide : rend la racine du dialog (content) hit-test opaque sur toute sa surface
        // mesurée (y compris le texte non interactif dedans), pour qu'un tap dessus ne retombe pas sur
        // le BoxView en dessous - centralisé ici plutôt que dupliqué dans les 27 dialogs.
        content.GestureRecognizers.Add(new TapGestureRecognizer());
    }

    private void OnBackdropTapped(object? sender, TappedEventArgs e) => _onBackdropTapped();
}
