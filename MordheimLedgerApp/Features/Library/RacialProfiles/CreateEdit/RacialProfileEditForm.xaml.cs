using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.RacialProfiles.CreateEdit;

public partial class RacialProfileEditForm : FormContent<bool>
{
    public RacialProfileEditForm(RacialProfileEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
