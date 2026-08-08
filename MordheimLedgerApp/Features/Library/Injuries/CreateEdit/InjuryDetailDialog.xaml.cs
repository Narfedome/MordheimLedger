using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

/// <summary>Pure XAML wrapper bound to InjuryDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class InjuryDetailDialog : Popup<bool>
{
    public InjuryDetailDialog(InjuryDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
