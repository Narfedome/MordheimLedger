using CommunityToolkit.Mvvm.ComponentModel;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>
    /// Base for read-only recap dialogs (WarbandArchetypeDetailDialog, ChipDetailDialog, ...) - each
    /// mirrors its own Edit dialog's layout with Label instead of Entry/Editor/Picker. Nothing to
    /// confirm here, only dismiss - the inherited CancelCommand (Close/X button) already covers that,
    /// no separate Confirm needed like ConfirmDialogViewModel.
    /// </summary>
    public abstract partial class ReadOnlyDialogViewModel : DialogViewModel<bool>
    {
        protected override bool CancelResult => false;

        [ObservableProperty]
        private string title = string.Empty;
    }
}
