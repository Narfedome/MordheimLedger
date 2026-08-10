using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

public partial class EquipmentListEditDialog : DialogContent<bool>
{
    public EquipmentListEditDialog(EquipmentListEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
    }
}
