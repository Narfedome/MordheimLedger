using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.RacialProfiles.CreateEdit;

public partial class RacialProfileEditDialog : DialogContent<bool>
{
    public RacialProfileEditDialog(RacialProfileEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
