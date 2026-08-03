using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to ActionSheetDialogViewModel: all logic lives there, not here.</summary>
public partial class ActionSheetDialog : Popup<int>
{
    public ActionSheetDialog(ActionSheetDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async index => await CloseAsync(index);
    }
}
