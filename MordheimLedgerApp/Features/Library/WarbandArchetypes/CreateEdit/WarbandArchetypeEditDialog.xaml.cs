using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

public partial class WarbandArchetypeEditDialog : DialogContent<bool>
{
    public WarbandArchetypeEditDialog(WarbandArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
