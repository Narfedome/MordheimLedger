namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to PromptDialogViewModel: all logic lives there, not here.</summary>
public partial class PromptDialog : DialogContent<string?>
{
    public PromptDialog(PromptDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;

        // Loaded (pas Opened - Popup-spécifique, disparu avec la conversion en DialogContent/ContentView,
        // voir DialogStack) : se déclenche aussi bien à l'ouverture racine qu'à un push imbriqué.
        Loaded += (_, _) =>
        {
            InputEntry.Focus();
            InputEntry.CursorPosition = InputEntry.Text?.Length ?? 0;
        };
    }
}
