namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

public partial class SellEquipmentSelectorPage : ContentPage
{
    public SellEquipmentSelectorPage(SellEquipmentSelectorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
