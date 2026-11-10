using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

public partial class InjuryEditForm : FormContent<bool>
{
    public InjuryEditForm(InjuryEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
