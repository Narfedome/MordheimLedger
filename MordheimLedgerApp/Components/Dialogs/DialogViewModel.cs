using CommunityToolkit.Mvvm.Input;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>
    /// Common base for dialog ViewModels (Confirm/Prompt/ActionSheet/...): each one redeclared the
    /// same CloseRequested event and the same Cancel command invoking it with a fixed "cancelled"
    /// value (false, null, -1...) — only the confirmation logic actually differs per dialog.
    /// </summary>
    public abstract partial class DialogViewModel<TResult> : BaseViewModel
    {
        /// <summary>Signals the XAML wrapper (ConfirmDialog, PromptDialog...) to close.</summary>
        public event Action<TResult>? CloseRequested;

        protected void Close(TResult result) => CloseRequested?.Invoke(result);

        /// <summary>Value sent on cancel-close (false, null, -1 depending on the dialog).</summary>
        protected abstract TResult CancelResult { get; }

        [RelayCommand]
        public void Cancel() => Close(CancelResult);
    }
}
