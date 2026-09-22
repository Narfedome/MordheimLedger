using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

public partial class DramatisPersonaEditDialog : DialogContent<bool>
{
    public DramatisPersonaEditDialog(DramatisPersonaEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
