using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.MagicSchools.CreateEdit;

public partial class MagicSchoolEditForm : FormContent<bool>
{
    public MagicSchoolEditForm(MagicSchoolEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
