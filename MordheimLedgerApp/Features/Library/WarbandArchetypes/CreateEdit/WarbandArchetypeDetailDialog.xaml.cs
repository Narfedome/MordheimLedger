using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>Pure XAML wrapper bound to WarbandArchetypeDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class WarbandArchetypeDetailDialog : Popup<bool>
{
    public WarbandArchetypeDetailDialog(WarbandArchetypeDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
