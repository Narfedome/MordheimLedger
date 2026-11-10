using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

public partial class WarbandArchetypeEditForm : FormContent<bool>
{
    public WarbandArchetypeEditForm(WarbandArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
