namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListPage : ContentPage
{
    public WarbandListPage(WarbandListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is WarbandListViewModel vm)
            vm.LoadWarbandsCommand.Execute(null);
    }
}
