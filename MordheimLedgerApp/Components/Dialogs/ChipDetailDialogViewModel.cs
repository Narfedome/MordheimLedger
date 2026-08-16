using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

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

        // Résolu via le service locator (même idiome que BaseViewModel.Loading) plutôt qu'injecté au
        // constructeur : ChipDetailDialogViewModel est instancié par DialogViewModel<TResult>.
        // ShowChipDetailAsync, partagé par ~15 dialogs Edit/ReadOnly qui n'ont eux-mêmes aucune raison
        // de connaître IDetailDialogService - seul ce popup imbriqué (le cas MagicSchool, RelatedSpells)
        // en a besoin.
        private IDetailDialogService? _detailDialogs;
        private IDetailDialogService DetailDialogs =>
            _detailDialogs ??= IPlatformApplication.Current!.Services.GetRequiredService<IDetailDialogService>();

        public ChipDetailDialogViewModel(string name, string? description, IReadOnlyList<Spell>? relatedSpells = null)
        {
            Title = name;
            Description = string.IsNullOrWhiteSpace(description) ? Loc["LibNoDescription"] : description;
            RelatedSpells = new ObservableCollection<Spell>(relatedSpells ?? Array.Empty<Spell>());
        }

        /// <summary>Recap complet (jet/difficulté/école) au lieu du mini-popup Nom+Description générique -
        /// même correctif que EquipmentItemDetailDialogViewModel.ShowSpecialRuleDetail pour les règles de
        /// matériau : un Sort a des attributs propres qu'un simple Nom+Description ne montre pas.</summary>
        [RelayCommand]
        private Task ShowSpellDetail(Spell spell) => DetailDialogs.ShowSpellDetailDialogAsync(spell);
    }
}
