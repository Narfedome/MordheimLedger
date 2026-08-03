namespace MordheimLedgerApp.Features.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Cf. commentaire XAML sur ContentStack : uniquement sur Desktop, pour ne pas risquer de
        // toucher au vrai défaut (PositiveInfinity) sur Android/iOS.
        // Pas de HorizontalOptions=Center ici (contrairement au commentaire XAML/DmTools d'origine) :
        // sur cette version de MAUI, VerticalStackLayout + MaximumWidthRequest + HorizontalOptions=
        // Center se mesure à sa taille NATURELLE (les colonnes "*" des Grid internes s'effondrent à
        // zéro sans contrainte de largeur) au lieu de remplir jusqu'au plafond - vérifié en comparant
        // la largeur réellement rendue (~240px de contenu sur une fenêtre de 600px) à celle de DmTools
        // (~380px) au pixel près. Sans Center, le HorizontalOptions par défaut (Fill) remplit jusqu'au
        // plafond ET reste centré quand la fenêtre est plus large que 560 (revérifié à 1300px).
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop)
        {
            ContentStack.MaximumWidthRequest = 560;
        }
    }
}
