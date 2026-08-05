using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

public partial class MutationEditDialog : Popup<bool>
{
    public MutationEditDialog(MutationEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
