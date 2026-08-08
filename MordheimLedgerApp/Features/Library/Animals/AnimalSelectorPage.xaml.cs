namespace MordheimLedgerApp.Features.Library.Animals;

public partial class AnimalSelectorPage : ContentPage
{
    public AnimalSelectorPage(AnimalViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is AnimalViewModel vm)
            await vm.InitializeAsync();
    }
}
