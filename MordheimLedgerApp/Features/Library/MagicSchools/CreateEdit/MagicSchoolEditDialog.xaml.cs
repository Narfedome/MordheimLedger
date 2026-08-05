using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.MagicSchools.CreateEdit;

public partial class MagicSchoolEditDialog : Popup<bool>
{
    public MagicSchoolEditDialog(MagicSchoolEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
