using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>Logic for the free-text prompt dialog (PromptDialog is just a XAML wrapper bound to it).</summary>
    public partial class PromptDialogViewModel : DialogViewModel<string?>
    {
        protected override string? CancelResult => null;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string message = string.Empty;

        [ObservableProperty]
        private string placeholder = string.Empty;

        [ObservableProperty]
        private string text = string.Empty;

        [ObservableProperty]
        private string confirmLabel = string.Empty;

        [ObservableProperty]
        private string cancelLabel = string.Empty;

        public bool HasMessage => !string.IsNullOrEmpty(Message);

        public PromptDialogViewModel(string title, string message, string placeholder, string initialValue, string confirmLabel, string cancelLabel)
        {
            Title = title;
            Message = message;
            Placeholder = placeholder;
            Text = initialValue;
            ConfirmLabel = confirmLabel;
            CancelLabel = cancelLabel;
        }

        [RelayCommand]
        public void Confirm() => Close(Text);
    }
}
