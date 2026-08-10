using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarriorEditDialog : DialogContent<bool>
{
    public WarriorEditDialog(WarriorEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
    }
}
