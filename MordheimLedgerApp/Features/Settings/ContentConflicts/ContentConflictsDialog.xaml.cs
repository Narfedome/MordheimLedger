using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Settings.ContentConflicts;

/// <summary>Pure XAML wrapper bound to ContentConflictsDialogViewModel: all logic lives there, not here.</summary>
public partial class ContentConflictsDialog : DialogContent<bool>
{
    public ContentConflictsDialog(ContentConflictsDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
