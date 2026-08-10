using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

/// <summary>Pure XAML wrapper bound to SpecialRuleDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class SpecialRuleDetailDialog : DialogContent<bool>
{
    public SpecialRuleDetailDialog(SpecialRuleDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
