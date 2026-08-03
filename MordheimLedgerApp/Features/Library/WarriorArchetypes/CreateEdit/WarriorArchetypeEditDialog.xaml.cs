using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

public partial class WarriorArchetypeEditDialog : Popup<bool>
{
    public WarriorArchetypeEditDialog(WarriorArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
