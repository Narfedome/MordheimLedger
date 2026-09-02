using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

public partial class DramatisPersonaPairDetailDialog : DialogContent<bool>
{
    public DramatisPersonaPairDetailDialog(DramatisPersonaPairDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
