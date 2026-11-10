using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

public partial class WarriorArchetypeEditForm : FormContent<bool>
{
    public WarriorArchetypeEditForm(WarriorArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
