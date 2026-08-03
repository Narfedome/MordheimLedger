using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

public partial class WarbandArchetypeEditDialog : Popup<bool>
{
    public WarbandArchetypeEditDialog(WarbandArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
