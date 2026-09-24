namespace MordheimLedgerApp.Features.Library.Races;

public partial class RaceListPage : ContentPage
{
    public RaceListPage(RaceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is RaceViewModel vm)
            await vm.InitializeAsync();
    }
}
