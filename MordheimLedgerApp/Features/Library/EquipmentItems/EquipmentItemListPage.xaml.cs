using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

public partial class EquipmentItemListPage : ContentPage
{
    public EquipmentItemListPage(EquipmentItemViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is EquipmentItemViewModel vm)
            await vm.InitializeAsync();
    }
}
