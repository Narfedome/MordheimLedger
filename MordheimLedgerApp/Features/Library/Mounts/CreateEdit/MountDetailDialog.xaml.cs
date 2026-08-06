using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.Mounts.CreateEdit;

/// <summary>Pure XAML wrapper bound to MountDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class MountDetailDialog : Popup<bool>
{
    public MountDetailDialog(MountDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
