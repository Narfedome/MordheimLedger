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

        if (BindingContext is MagicSchoolViewModel vm)
            await vm.InitializeAsync();
    }
}
