namespace MordheimLedgerApp.Features.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Cf. commentaire XAML sur ContentStack : uniquement sur Desktop, pour ne pas risquer de
        // toucher au vrai défaut (PositiveInfinity) sur Android/iOS.
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop)
        {
            ContentStack.MaximumWidthRequest = 560;
            ContentStack.HorizontalOptions = LayoutOptions.Center;
        }
    }
}
