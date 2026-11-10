using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

public partial class EquipmentItemEditForm : FormContent<bool>
{
    public EquipmentItemEditForm(EquipmentItemEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
