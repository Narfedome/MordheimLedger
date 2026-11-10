using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

public partial class MutationEditForm : FormContent<bool>
{
    public MutationEditForm(MutationEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
