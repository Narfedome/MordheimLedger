using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

public partial class DramatisPersonaDetailDialog : DialogContent<bool>
{
    public DramatisPersonaDetailDialog(DramatisPersonaDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
