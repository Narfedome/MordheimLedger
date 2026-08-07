using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

/// <summary>Pure XAML wrapper bound to WarriorArchetypeDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class WarriorArchetypeDetailDialog : Popup<bool>
{
    public WarriorArchetypeDetailDialog(WarriorArchetypeDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
