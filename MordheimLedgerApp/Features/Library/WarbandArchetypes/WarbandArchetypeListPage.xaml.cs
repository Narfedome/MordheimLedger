using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeListPage : ContentPage
{
    public WarbandArchetypeListPage(WarbandArchetypeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is WarbandArchetypeViewModel vm)
            await vm.InitializeAsync();
    }
}
