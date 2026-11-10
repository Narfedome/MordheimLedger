using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

public partial class DramatisPersonaEditForm : FormContent<bool>
{
    public DramatisPersonaEditForm(DramatisPersonaEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
