namespace MordheimLedgerApp.Features.Library.Skills;

public partial class SkillSelectorPage : ContentPage
{
    public SkillSelectorPage(SkillViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is SkillViewModel vm)
            await vm.InitializeAsync();
    }
}
