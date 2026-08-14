using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>Pure XAML wrapper bound to WarbandArchetypeDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class WarbandArchetypeDetailDialog : DialogContent<bool>
{
    public WarbandArchetypeDetailDialog(WarbandArchetypeDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
