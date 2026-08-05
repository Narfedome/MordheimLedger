namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeSelectorPage : ContentPage
{
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
