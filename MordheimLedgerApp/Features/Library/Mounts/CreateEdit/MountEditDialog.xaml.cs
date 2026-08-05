using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Mounts.CreateEdit;

public partial class MountEditDialog : Popup<bool>
{
    public MountEditDialog(MountEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
