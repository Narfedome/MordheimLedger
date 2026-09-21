namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Pure XAML wrapper bound to SellEquipmentDialogViewModel: all logic lives there, not here.</summary>
public partial class SellEquipmentDialog : Components.Dialogs.DialogContent<bool>
{
    public SellEquipmentDialog(SellEquipmentDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
