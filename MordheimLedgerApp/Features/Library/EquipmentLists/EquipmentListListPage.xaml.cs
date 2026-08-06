using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library.EquipmentLists;

public partial class EquipmentListListPage : ContentPage
{
    public EquipmentListListPage(EquipmentListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is EquipmentListViewModel vm)
            await vm.InitializeAsync();
    }
}
