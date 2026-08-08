using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to ChipDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class ChipDetailDialog : Popup<bool>
{
    public ChipDetailDialog(ChipDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
