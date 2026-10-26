using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Pure XAML wrapper bound to WarriorPhotoDialogViewModel: all logic lives there, not here.</summary>
public partial class WarriorPhotoDialog : DialogContent<bool>
{
    public WarriorPhotoDialog(WarriorPhotoDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
