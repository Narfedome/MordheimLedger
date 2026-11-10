using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;

public partial class HiredSwordEditForm : FormContent<bool>
{
    public HiredSwordEditForm(HiredSwordEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
