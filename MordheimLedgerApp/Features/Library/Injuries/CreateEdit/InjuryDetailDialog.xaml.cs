using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

/// <summary>Pure XAML wrapper bound to InjuryDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class InjuryDetailDialog : DialogContent<bool>
{
    public InjuryDetailDialog(InjuryDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
