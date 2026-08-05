using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

public partial class SpecialRuleEditDialog : Popup<bool>
{
    public SpecialRuleEditDialog(SpecialRuleEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
