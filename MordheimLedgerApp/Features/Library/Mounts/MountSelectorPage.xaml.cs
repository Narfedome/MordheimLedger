namespace MordheimLedgerApp.Features.Library.Mounts;

public partial class MountSelectorPage : ContentPage
{
    public MountSelectorPage(MountViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MountViewModel vm)
            await vm.InitializeAsync();
    }
}
