using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Races.CreateEdit;

public partial class RaceEditDialog : DialogContent<bool>
{
    public RaceEditDialog(RaceEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
