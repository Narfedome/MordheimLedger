using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

/// <summary>Pure XAML wrapper bound to EquipmentItemDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class EquipmentItemDetailDialog : DialogContent<bool>
{
    public EquipmentItemDetailDialog(EquipmentItemDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
