namespace MordheimLedgerApp.Features.Library.MagicSchools;

public partial class MagicSchoolSelectorPage : ContentPage
{
    public MagicSchoolSelectorPage(MagicSchoolViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is MagicSchoolViewModel vm)
            await Services.UiErrors.RunAsync(() => vm.InitializeAsync(), GetType().Name);
    }
}
