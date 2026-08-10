using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

/// <summary>Pure XAML wrapper bound to EquipmentListDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class EquipmentListDetailDialog : DialogContent<bool>
{
    public EquipmentListDetailDialog(EquipmentListDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
