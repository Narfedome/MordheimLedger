namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeSelectorPage : ContentPage
{
    public static readonly BindableProperty SelectionModeProperty =
    BindableProperty.Create(
        nameof(SelectionMode),
        typeof(SelectionMode),
        typeof(WarbandArchetypeSelectorPage),
        SelectionMode.None);

    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set
        {
            SetValue(SelectionModeProperty, value);

            if (BindingContext is WarbandArchetypeViewModel vm)
                vm.SelectionMode = value;

            // "Choisir des bandes" n'a de sens qu'en sélection multiple (restrictions par bande) ;
            // le choix du type de bande à la création n'en prend qu'une.
            PickerLayout.Title = Services.LocalizationService.Instance[value == SelectionMode.Single
                ? "WarbandArchetypeSelectSingleTitle"
                : "WarbandArchetypeSelectTitle"];
        }
    }
    public WarbandArchetypeSelectorPage(WarbandArchetypeViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is WarbandArchetypeViewModel vm)
            await vm.InitializeAsync();
    }
}
