using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

public partial class SpecialRuleEditForm : FormContent<bool>
{
    public SpecialRuleEditForm(SpecialRuleEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
