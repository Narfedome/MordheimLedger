namespace MordheimLedgerApp.Features.Library.Injuries;

public partial class InjurySelectorPage : ContentPage
{
    public InjurySelectorPage(InjuryViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is InjuryViewModel vm)
            await vm.InitializeAsync();
    }
}
