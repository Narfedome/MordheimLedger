using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library.Injuries;

public partial class InjuryListPage : ContentPage
{
    public InjuryListPage(InjuryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is InjuryViewModel vm)
            await vm.InitializeAsync();
    }
}
