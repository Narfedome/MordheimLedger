namespace MordheimLedgerApp.Features.Library.HiredSwords;

public partial class HiredSwordSelectorPage : ContentPage
{
    /// <summary>Single (ex. "Une Faveur Rendue" - un seul Franc-Tireur gratuit) vs Multiple (ex. l'étape
    /// Mercenaires du wizard de création - engager plusieurs types d'un coup) - même bascule que
    /// WarbandArchetypeSelectorPage.SelectionMode.</summary>
    public static readonly BindableProperty SelectionModeProperty =
        BindableProperty.Create(
            nameof(SelectionMode),
            typeof(SelectionMode),
            typeof(HiredSwordSelectorPage),
            SelectionMode.None);

    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set
        {
            SetValue(SelectionModeProperty, value);

            if (BindingContext is HiredSwordViewModel vm)
                vm.SelectionMode = value;
        }
    }

    public HiredSwordSelectorPage(HiredSwordViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HiredSwordViewModel vm)
            await vm.InitializeAsync();
    }
}
