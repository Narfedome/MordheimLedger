using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

public partial class EquipmentItemEditDialog : DialogContent<bool>
{
    public EquipmentItemEditDialog(EquipmentItemEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
