namespace MordheimLedgerApp.Features.Library.Mutations;

public partial class MutationSelectorPage : ContentPage
{
    public MutationSelectorPage(MutationViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MutationViewModel vm)
            await vm.InitializeAsync();
    }
}
