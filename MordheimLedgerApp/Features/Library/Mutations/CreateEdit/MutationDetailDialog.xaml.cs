using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

/// <summary>Pure XAML wrapper bound to MutationDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class MutationDetailDialog : DialogContent<bool>
{
    public MutationDetailDialog(MutationDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
