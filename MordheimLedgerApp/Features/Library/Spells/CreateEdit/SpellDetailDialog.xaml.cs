using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

/// <summary>Pure XAML wrapper bound to SpellDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class SpellDetailDialog : Popup<bool>
{
    public SpellDetailDialog(SpellDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
