namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>XAML wrapper bound to MaterialPickerDialogViewModel: all logic lives there, except the tap on
/// the "Bénie" label toggling its checkbox (a larger touch target than the checkbox alone).</summary>
public partial class MaterialPickerDialog : Components.Dialogs.DialogContent<bool>
{
    public MaterialPickerDialog(MaterialPickerDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnBlessedLabelTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: MaterialChoice choice })
            choice.IsBlessed = !choice.IsBlessed;
    }
}
