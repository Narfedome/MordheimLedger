namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandDetailPage : ContentPage
{
    public WarbandDetailPage(WarbandDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
