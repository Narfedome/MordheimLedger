using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>
    /// Shared mini recap for a tapped chip (restriction/SpecialRule/MagicSchool chip inside any of the
    /// 8 XxxDetailDialogs) - a chip is always a SpecialRule/MagicSchool/WarbandArchetype/WarriorArchetype,
    /// and all four only ever need Name + Description shown here, so one dialog covers every chip
    /// instead of one per source type. relatedSpells is the one exception: null/empty for every chip
    /// type except MagicSchool (Warband's ShowMagicSchoolDetail resolves and passes it) - kept optional
    /// on this shared VM rather than forking a MagicSchool-only dialog, same "one generic popup" stance.
    /// </summary>
    public partial class ChipDetailDialogViewModel : ReadOnlyDialogViewModel
    {
        [ObservableProperty]
        private string description = string.Empty;

        public ObservableCollection<Spell> RelatedSpells { get; }

        public bool HasRelatedSpells => RelatedSpells.Count > 0;

        public ChipDetailDialogViewModel(string name, string? description, IReadOnlyList<Spell>? relatedSpells = null)
        {
            Title = name;
            Description = string.IsNullOrWhiteSpace(description) ? Loc["LibNoDescription"] : description;
            RelatedSpells = new ObservableCollection<Spell>(relatedSpells ?? Array.Empty<Spell>());
        }

        [RelayCommand]
        private Task ShowSpellDetail(Spell spell) => ShowChipDetailAsync(spell.Name, spell.Description);
    }
}
