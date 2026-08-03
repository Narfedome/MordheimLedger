using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to PromptDialogViewModel: all logic lives there, not here.</summary>
public partial class PromptDialog : Popup<string?>
{
    public PromptDialog(PromptDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);

        Opened += (_, _) =>
        {
            InputEntry.Focus();
            InputEntry.CursorPosition = InputEntry.Text?.Length ?? 0;
        };
    }
}
