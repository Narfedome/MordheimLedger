namespace MordheimLedgerApp.Features.Library.MagicSchools;

public partial class MagicSchoolListPage : ContentPage
{
    public MagicSchoolListPage(MagicSchoolViewModel viewModel)
    {
        InitializeComponent();
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
