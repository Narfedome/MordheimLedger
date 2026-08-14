using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

public partial class SpecialRuleEditDialog : DialogContent<bool>
{
    public SpecialRuleEditDialog(SpecialRuleEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
