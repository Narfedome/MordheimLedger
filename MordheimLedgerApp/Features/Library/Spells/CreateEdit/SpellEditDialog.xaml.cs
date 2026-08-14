using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

public partial class SpellEditDialog : DialogContent<bool>
{
    public SpellEditDialog(SpellEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
