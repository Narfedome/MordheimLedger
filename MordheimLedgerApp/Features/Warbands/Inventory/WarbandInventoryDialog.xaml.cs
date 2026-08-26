using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.Inventory;

/// <summary>Pure XAML wrapper bound to WarbandInventoryDialogViewModel: all logic lives there, not here.</summary>
public partial class WarbandInventoryDialog : DialogContent<bool>
{
    public WarbandInventoryDialog(WarbandInventoryDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
