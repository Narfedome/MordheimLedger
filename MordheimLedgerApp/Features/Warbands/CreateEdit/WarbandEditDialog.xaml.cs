using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarbandEditDialog : DialogContent<bool>
{
    public WarbandEditDialog(WarbandEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
