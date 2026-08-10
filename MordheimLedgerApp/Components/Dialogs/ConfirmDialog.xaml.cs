namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to ConfirmDialogViewModel: all logic lives there, not here.</summary>
public partial class ConfirmDialog : DialogContent<bool>
{
    public ConfirmDialog(ConfirmDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
