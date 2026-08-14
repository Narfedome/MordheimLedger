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

        if (BindingContext is WarbandArchetypeViewModel vm)
            await vm.InitializeAsync();
    }
}
