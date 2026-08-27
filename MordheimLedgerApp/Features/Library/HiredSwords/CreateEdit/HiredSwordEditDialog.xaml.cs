using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;

public partial class HiredSwordEditDialog : DialogContent<bool>
{
    public HiredSwordEditDialog(HiredSwordEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
