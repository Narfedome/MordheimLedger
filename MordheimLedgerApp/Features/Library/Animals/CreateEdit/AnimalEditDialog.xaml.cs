using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Animals.CreateEdit;

public partial class AnimalEditDialog : DialogContent<bool>
{
    public AnimalEditDialog(AnimalEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ContentScroll.MaximumHeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
    }
}
