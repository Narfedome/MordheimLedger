using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>Pure XAML wrapper bound to StartGameDialogViewModel: all logic lives there, not here.</summary>
public partial class StartGameDialog : DialogContent<bool>
{
    public StartGameDialog(StartGameDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
