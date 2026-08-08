using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

/// <summary>Pure XAML wrapper bound to SpecialRuleDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class SpecialRuleDetailDialog : Popup<bool>
{
    public SpecialRuleDetailDialog(SpecialRuleDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
