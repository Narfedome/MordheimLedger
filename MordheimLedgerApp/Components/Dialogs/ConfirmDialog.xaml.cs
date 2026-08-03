using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to ConfirmDialogViewModel: all logic lives there, not here.</summary>
public partial class ConfirmDialog : Popup<bool>
{
    public ConfirmDialog(ConfirmDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
