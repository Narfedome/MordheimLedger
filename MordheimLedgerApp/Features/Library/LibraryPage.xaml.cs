using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library;

public partial class LibraryPage : ContentPage
{
    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is LibraryViewModel vm)
            await vm.InitializeAsync();
    }
}
