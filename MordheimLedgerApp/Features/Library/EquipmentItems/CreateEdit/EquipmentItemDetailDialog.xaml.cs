using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

/// <summary>Pure XAML wrapper bound to EquipmentItemDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class EquipmentItemDetailDialog : Popup<bool>
{
    public EquipmentItemDetailDialog(EquipmentItemDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
