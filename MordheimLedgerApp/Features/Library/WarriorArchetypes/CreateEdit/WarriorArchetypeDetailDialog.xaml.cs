using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

/// <summary>Pure XAML wrapper bound to WarriorArchetypeDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class WarriorArchetypeDetailDialog : DialogContent<bool>
{
    public WarriorArchetypeDetailDialog(WarriorArchetypeDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
