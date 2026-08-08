using CommunityToolkit.Maui.Views;

namespace MordheimLedgerApp.Features.Library.Animals.CreateEdit;

/// <summary>Pure XAML wrapper bound to AnimalDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class AnimalDetailDialog : Popup<bool>
{
    public AnimalDetailDialog(AnimalDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
