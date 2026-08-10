namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to ChipDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class ChipDetailDialog : DialogContent<bool>
{
    public ChipDetailDialog(ChipDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
