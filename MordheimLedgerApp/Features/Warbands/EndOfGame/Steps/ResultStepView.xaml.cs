namespace MordheimLedgerApp.Features.Warbands.EndOfGame.Steps;

public partial class ResultStepView : ContentView
{
    public ResultStepView()
    {
        InitializeComponent();

        // Contourne un bug d'affichage WinUI (retour utilisateur 2026-09-22 - "j'ai toujours pas
        // d'affichage de la valeur sur la combobox de victoire") : SelectedResult est déjà correct dès
        // la construction du ViewModel (voir EndOfGamePageViewModel's constructor), mais le ComboBox
        // natif WinUI ne reflète jamais un SelectedItem posé avant que SON PROPRE handler natif
        // n'existe. Un essai précédent (retoucher SelectedResult depuis EndOfGamePage.OnAppearing) n'a
        // pas suffi - le cycle de vie de la PAGE n'est pas une garantie que CE contrôle précis a déjà
        // son handler natif. Picker.Loaded (VisualElement.Loaded, disponible depuis .NET MAUI 7) est
        // l'événement natif propre à CE Picker : à ce moment précis, son handler existe à coup sûr.
        ResultPicker.Loaded += (_, _) =>
        {
            if (BindingContext is EndOfGamePageViewModel vm)
                vm.RefreshResultPickerDisplay();
        };
    }
}
