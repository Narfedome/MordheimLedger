using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

public partial class MutationEditDialog : DialogContent<bool>
{
    public MutationEditDialog(MutationEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
