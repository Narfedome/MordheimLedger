using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;

public partial class HiredSwordDetailDialog : DialogContent<bool>
{
    public HiredSwordDetailDialog(HiredSwordDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
