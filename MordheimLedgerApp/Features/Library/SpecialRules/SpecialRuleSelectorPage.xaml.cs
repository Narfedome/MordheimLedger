namespace MordheimLedgerApp.Features.Library.SpecialRules;

public partial class SpecialRuleSelectorPage : ContentPage
{
    public SpecialRuleSelectorPage(SpecialRuleViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is SpecialRuleViewModel vm)
            await vm.InitializeAsync();
    }
}
