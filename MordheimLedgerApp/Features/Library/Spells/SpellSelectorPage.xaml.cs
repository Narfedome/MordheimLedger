namespace MordheimLedgerApp.Features.Library.Spells;

public partial class SpellSelectorPage : ContentPage
{
    public SpellSelectorPage(SpellViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is SpellViewModel vm)
            await vm.InitializeAsync();
    }
}
