namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to ActionSheetDialogViewModel: all logic lives there, not here.</summary>
public partial class ActionSheetDialog : DialogContent<int>
{
    public ActionSheetDialog(ActionSheetDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
