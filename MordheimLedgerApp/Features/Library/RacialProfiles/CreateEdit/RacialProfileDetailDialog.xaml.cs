using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.RacialProfiles.CreateEdit;

public partial class RacialProfileDetailDialog : DialogContent<bool>
{
    public RacialProfileDetailDialog(RacialProfileDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
