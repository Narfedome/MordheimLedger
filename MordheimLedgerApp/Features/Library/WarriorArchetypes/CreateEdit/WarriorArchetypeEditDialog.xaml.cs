using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

public partial class WarriorArchetypeEditDialog : DialogContent<bool>
{
    public WarriorArchetypeEditDialog(WarriorArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
    }
}
