namespace MordheimLedgerApp.Features.Library.EquipmentItems;

public partial class EquipmentItemSelectorPage : ContentPage
{
    public EquipmentItemSelectorPage(EquipmentItemViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is EquipmentItemViewModel vm)
            await vm.InitializeAsync();
    }
}
