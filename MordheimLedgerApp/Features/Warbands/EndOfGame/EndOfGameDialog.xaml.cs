using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

public partial class EndOfGameDialog : DialogContent<bool>
{
    public EndOfGameDialog(EndOfGameDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
