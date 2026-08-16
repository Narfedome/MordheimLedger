using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Pure XAML wrapper bound to SpellRollDialogViewModel: all logic lives there, not here.</summary>
public partial class SpellRollDialog : Components.Dialogs.DialogContent<Spell?>
{
    public SpellRollDialog(SpellRollDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
