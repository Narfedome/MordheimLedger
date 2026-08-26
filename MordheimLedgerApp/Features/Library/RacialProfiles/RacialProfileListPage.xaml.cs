namespace MordheimLedgerApp.Features.Library.RacialProfiles;

public partial class RacialProfileListPage : ContentPage
{
    public RacialProfileListPage(RacialProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is RacialProfileViewModel vm)
            await vm.InitializeAsync();
    }
}
