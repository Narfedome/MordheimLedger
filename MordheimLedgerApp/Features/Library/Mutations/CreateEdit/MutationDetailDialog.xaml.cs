using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

/// <summary>Pure XAML wrapper bound to MutationDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class MutationDetailDialog : Popup<bool>
{
    public MutationDetailDialog(MutationDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
