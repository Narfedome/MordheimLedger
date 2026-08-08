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

        /// <summary>Opens the shared chip mini-popup - every XxxDetailDialog's chip-tap commands funnel
        /// through here instead of duplicating the ShowDialogAsync/ChipDetailDialog wiring 8 times.</summary>
        protected async Task ShowChipDetailAsync(string name, string? description) =>
            await ShowDialogAsync(new ChipDetailDialog(new ChipDetailDialogViewModel(name, description)));
    }
}
