using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

/// <summary>Pure XAML wrapper bound to SpellDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class SpellDetailDialog : DialogContent<bool>
{
    public SpellDetailDialog(SpellDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
