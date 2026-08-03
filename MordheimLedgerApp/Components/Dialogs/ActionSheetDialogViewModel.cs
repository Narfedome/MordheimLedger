using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>An option in the list, with its original index: needed to disambiguate two options
    /// with the same label once bound (e.g. two homonymous warbands) — list position alone isn't
    /// enough once data binding is in play.</summary>
    public record ActionSheetOption(int Index, string Label);

    /// <summary>
    /// Logic for the generic choice list (ActionSheetDialog is just a XAML wrapper bound to it).
    /// Closes with the chosen option's index (-1: cancelled).
    /// </summary>
    public partial class ActionSheetDialogViewModel : DialogViewModel<int>
    {
        protected override int CancelResult => -1;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string cancelLabel = string.Empty;

        public List<ActionSheetOption> Options { get; }

        public ActionSheetDialogViewModel(string title, IEnumerable<string> options, string cancelLabel)
        {
            Title = title;
            CancelLabel = cancelLabel;
            Options = options.Select((label, index) => new ActionSheetOption(index, label)).ToList();
        }

        [RelayCommand]
        public void Select(ActionSheetOption option) => Close(option.Index);
    }
}
