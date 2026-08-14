using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.MagicSchools.CreateEdit;

public partial class MagicSchoolEditDialog : DialogContent<bool>
{
    public MagicSchoolEditDialog(MagicSchoolEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
