using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes;

public partial class WarriorArchetypeListPage : ContentPage
{
    public WarriorArchetypeListPage(WarriorArchetypeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is WarriorArchetypeViewModel vm)
            await vm.InitializeAsync();
    }
}
