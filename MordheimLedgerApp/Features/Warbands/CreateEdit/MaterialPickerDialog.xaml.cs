namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Pure XAML wrapper bound to MaterialPickerDialogViewModel: all logic lives there, not here.</summary>
public partial class MaterialPickerDialog : Components.Dialogs.DialogContent<bool>
{
    public MaterialPickerDialog(MaterialPickerDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
