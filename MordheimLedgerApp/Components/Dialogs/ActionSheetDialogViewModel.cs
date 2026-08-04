using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>An option in the list, with its original index: needed to disambiguate two options
    /// with the same label once bound (e.g. two homonymous warbands) — list position alone isn't
    /// enough once data binding is in play. IsHeader: a non-selectable section label (e.g. "Héros")
    /// rendered as plain text instead of a button - Index is unused (kept at -1) on those.</summary>
    public record ActionSheetOption(int Index, string Label, bool IsHeader = false);

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
            : this(title, options.Select((label, index) => new ActionSheetOption(index, label)), cancelLabel)
        {
        }

        /// <summary>Pre-built option list - lets a caller mix in header rows (see WarbandDetailViewModel's
        /// grouped Hero/Henchman recruit list).</summary>
        public ActionSheetDialogViewModel(string title, IEnumerable<ActionSheetOption> options, string cancelLabel)
        {
            Title = title;
            CancelLabel = cancelLabel;
            Options = options.ToList();
        }

        [RelayCommand]
        public void Select(ActionSheetOption option)
        {
            if (option.IsHeader) return;
            Close(option.Index);
        }
    }
}
