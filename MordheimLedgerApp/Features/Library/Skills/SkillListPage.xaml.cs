using CommunityToolkit.Maui.Extensions;

namespace MordheimLedgerApp.Features.Library.Skills;

public partial class SkillListPage : ContentPage
{
    public SkillListPage(SkillViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.WasPreviousPageACommunityToolkitPopupPage())
            return;

        if (BindingContext is SkillViewModel vm)
            await vm.InitializeAsync();
    }
}
