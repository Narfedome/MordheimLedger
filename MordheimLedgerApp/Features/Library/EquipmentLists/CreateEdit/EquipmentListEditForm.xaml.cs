using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

public partial class EquipmentListEditForm : FormContent<bool>
{
    public EquipmentListEditForm(EquipmentListEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
