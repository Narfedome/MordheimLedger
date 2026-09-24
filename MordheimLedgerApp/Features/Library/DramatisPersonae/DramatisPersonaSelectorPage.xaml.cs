namespace MordheimLedgerApp.Features.Library.DramatisPersonae;

public partial class DramatisPersonaSelectorPage : ContentPage
{
    public DramatisPersonaSelectorPage(DramatisPersonaViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsSelectorMode = true;
        viewModel.SelectionMode = SelectionMode.Single;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Components.Dialogs.DialogStack.Instance.IsClosingReadOnlyDialog) return;

        if (BindingContext is DramatisPersonaViewModel vm)
            await vm.InitializeAsync();
    }
}
