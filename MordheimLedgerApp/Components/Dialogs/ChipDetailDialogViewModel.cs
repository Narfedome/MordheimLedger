using CommunityToolkit.Mvvm.ComponentModel;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>
    /// Shared mini recap for a tapped chip (restriction/SpecialRule/MagicSchool chip inside any of the
    /// 8 XxxDetailDialogs) - a chip is always a SpecialRule/MagicSchool/WarbandArchetype/WarriorArchetype,
    /// and all four only ever need Name + Description shown here, so one dialog covers every chip
    /// instead of one per source type.
    /// </summary>
    public partial class ChipDetailDialogViewModel : ReadOnlyDialogViewModel
    {
        [ObservableProperty]
        private string description = string.Empty;

        public ChipDetailDialogViewModel(string name, string? description)
        {
            Title = name;
            Description = string.IsNullOrWhiteSpace(description) ? Loc["LibNoDescription"] : description;
        }
    }
}
