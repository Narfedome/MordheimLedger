namespace MordheimLedgerApp.Features.Library.WarriorArchetypes;

public partial class WarriorArchetypeSelectorPage : ContentPage
{
    public WarriorArchetypeSelectorPage(WarriorArchetypeSelectorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is WarriorArchetypeSelectorViewModel vm)
            await vm.InitializeAsync();
    }
}
