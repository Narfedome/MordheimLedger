using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

public partial class MutationEditDialog : DialogContent<bool>
{
    public MutationEditDialog(MutationEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
    }
}
