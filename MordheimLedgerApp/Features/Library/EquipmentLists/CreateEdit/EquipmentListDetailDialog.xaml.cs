using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

/// <summary>Pure XAML wrapper bound to EquipmentListDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class EquipmentListDetailDialog : Popup<bool>
{
    public EquipmentListDetailDialog(EquipmentListDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
