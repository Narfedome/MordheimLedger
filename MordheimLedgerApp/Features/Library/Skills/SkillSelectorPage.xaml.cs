namespace MordheimLedgerApp.Features.Library.Skills;

public partial class SkillSelectorPage : ContentPage
{
    public SkillSelectorPage(SkillViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is SkillViewModel vm)
            await vm.InitializeAsync();
    }
}
