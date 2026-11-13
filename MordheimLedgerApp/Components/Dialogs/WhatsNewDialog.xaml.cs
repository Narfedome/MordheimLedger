namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to WhatsNewDialogViewModel: all logic lives there, not here.</summary>
public partial class WhatsNewDialog : DialogContent<bool>
{
    public WhatsNewDialog(WhatsNewDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
