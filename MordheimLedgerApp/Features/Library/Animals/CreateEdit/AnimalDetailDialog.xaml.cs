using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Animals.CreateEdit;

/// <summary>Pure XAML wrapper bound to AnimalDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class AnimalDetailDialog : DialogContent<bool>
{
    public AnimalDetailDialog(AnimalDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
