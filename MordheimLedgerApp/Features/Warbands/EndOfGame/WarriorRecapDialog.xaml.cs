using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

public partial class WarriorRecapDialog : DialogContent<bool>
{
    public WarriorRecapDialog(WarriorRecapDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
