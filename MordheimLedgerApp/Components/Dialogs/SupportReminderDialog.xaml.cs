using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Pure XAML wrapper bound to SupportReminderDialogViewModel: all logic lives there, not here.</summary>
public partial class SupportReminderDialog : DialogContent<SupportReminderChoice>
{
    public SupportReminderDialog(SupportReminderDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
