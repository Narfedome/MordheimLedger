using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

public partial class InjuryEditDialog : DialogContent<bool>
{
    public InjuryEditDialog(InjuryEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
