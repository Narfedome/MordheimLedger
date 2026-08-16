using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

[QueryProperty(nameof(WarbandId), "warbandId")]
public partial class WarbandDetailViewModel : BaseViewModel
{
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly IInjuryPickerService _injuryPicker;
    private readonly ISpellPickerService _spellPicker;
    private readonly IMutationPickerService _mutationPicker;
    private readonly IAnimalPickerService _animalPicker;

    private List<WarriorArchetype> _recruitableArchetypes = new();
    private Dictionary<int, string> _archetypeNames = new();
    private List<SpecialRule> _bandWideSpecialRules = new();
    private List<MagicSchool> _bandMagicSchools = new();

    [ObservableProperty]
    private int warbandId;

    [ObservableProperty]
    private Warband? warband;

    /// <summary>Rulebook "calculate the warband rating" - sum over Heroes+Henchmen (active roster, dead
    /// warriors excluded) of (IsLargeCreature ? 20 : 5) + Experience. Recomputed after every LoadAsync -
    /// see IWarbandService.GetWarbandRatingAsync for the equivalent lightweight query used by
    /// WarbandListViewModel, which doesn't otherwise load the full roster.</summary>
    [ObservableProperty]
    private int rating;

    [ObservableProperty]
    private ObservableCollection<WarriorRow> heroes = new();

    [ObservableProperty]
    private ObservableCollection<WarriorRow> henchmen = new();

    [ObservableProperty]
    private ObservableCollection<WarriorRow> deadWarriors = new();

    [ObservableProperty]
    private bool heroesExpanded = true;

    [ObservableProperty]
    private bool henchmenExpanded = true;

    [ObservableProperty]
    private bool deadExpanded;

    [ObservableProperty]
    private ObservableCollection<HistoryEntry> historyEntries = new();

    [ObservableProperty]
    private bool showHistory;

    public WarbandDetailViewModel(IWarbandService warbandService, ILibraryService libraryService, IDetailDialogService detailDialogs,
        IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker, IInjuryPickerService injuryPicker,
        ISpellPickerService spellPicker, IMutationPickerService mutationPicker, IAnimalPickerService animalPicker)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _injuryPicker = injuryPicker;
        _spellPicker = spellPicker;
        _mutationPicker = mutationPicker;
        _animalPicker = animalPicker;

        // Le roster affiche des noms d'Équipement/Compétences/Blessures résolus dans la langue courante
        // - sans ça, ils resteraient périmés si la langue change pendant que cette page est déjà
        // affichée (même besoin que les pages Bibliothèque, voir WarbandArchetypeViewModel).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) =>
        {
            var vm = (WarbandDetailViewModel)r;
            if (vm.Warband is not null) _ = vm.LoadAsync(vm.WarbandId);
        });
    }

    partial void OnWarbandIdChanged(int value) => _ = LoadAsync(value);

    [RelayCommand]
    private void ToggleHeroes() => HeroesExpanded = !HeroesExpanded;

    [RelayCommand]
    private void ToggleHenchmen() => HenchmenExpanded = !HenchmenExpanded;

    [RelayCommand]
    private void ToggleDead() => DeadExpanded = !DeadExpanded;

    private async Task LoadAsync(int id)
    {
        await Loading.RunAsync(async () =>
        {
            Warband = await _warbandService.GetWarbandAsync(id);
            if (Warband is null) return;

            _recruitableArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            _archetypeNames = _recruitableArchetypes.ToDictionary(a => a.Id, a => a.Name);
            var warbandArchetype = await _libraryService.GetWarbandArchetypeAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            _bandWideSpecialRules = warbandArchetype?.SpecialRules ?? new List<SpecialRule>();
            _bandMagicSchools = warbandArchetype?.MagicSchools ?? new List<MagicSchool>();

            var loaded = await _warbandService.GetWarriorsAsync(id, LocalizationService.Instance.Language);
            var rows = loaded.Select(ToRow).ToList();
            Heroes = new ObservableCollection<WarriorRow>(rows.Where(r => r.Warrior.IsHero && !r.IsDead));
            Henchmen = new ObservableCollection<WarriorRow>(rows.Where(r => !r.Warrior.IsHero && !r.IsDead));
            DeadWarriors = new ObservableCollection<WarriorRow>(rows.Where(r => r.IsDead));
            Rating = Heroes.Concat(Henchmen).Sum(r => (r.Warrior.IsLargeCreature ? 20 : 5) + r.Warrior.Experience);

            var history = await _warbandService.GetHistoryEntriesAsync(id);
            HistoryEntries = new ObservableCollection<HistoryEntry>(history);
        });
    }

    private WarriorRow ToRow(Warrior warrior)
    {
        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == warrior.WarriorArchetypeId);
        var archetypeRules = archetype?.SpecialRules ?? new List<SpecialRule>();
        var mergedRules = _bandWideSpecialRules.Concat(archetypeRules).DistinctBy(r => r.Id);
        // Un lanceur de sorts pioche dans les écoles de SA bande (pas d'affiliation propre au guerrier) -
        // voir WarriorRow.MagicSchools. Vide pour tout autre guerrier.
        var magicSchools = archetype?.IsSpellcaster == true ? _bandMagicSchools : null;
        return new WarriorRow(warrior, _archetypeNames.GetValueOrDefault(warrior.WarriorArchetypeId, "?"), mergedRules, magicSchools);
    }

    [RelayCommand]
    private static async Task BackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void ShowRoster() => ShowHistory = false;

    [RelayCommand]
    private void ShowHistoryTab() => ShowHistory = true;

    [RelayCommand]
    private async Task RecruitWarriorAsync()
    {
        if (Warband is null) return;
        if (_recruitableArchetypes.Count == 0)
        {
            await ShowInfoAsync(Loc["WarriorsEmptyLibraryTitle"], Loc["WarriorsEmptyLibraryMessage"]);
            return;
        }

        var heroArchetypes = _recruitableArchetypes.Where(a => a.IsHero).ToList();
        var henchmanArchetypes = _recruitableArchetypes.Where(a => !a.IsHero).ToList();

        // Une seule liste (un warband n'a jamais assez de types recrutables pour justifier un écran à
        // part) avec des en-têtes Héros/Hommes de main non sélectionnables - seulement si les deux
        // groupes ont des types, sinon la liste reste plate.
        var candidates = new List<WarriorArchetype>();
        var sheetOptions = new List<ActionSheetOption>();
        var showHeaders = heroArchetypes.Count > 0 && henchmanArchetypes.Count > 0;

        void AddGroup(string headerKey, List<WarriorArchetype> group)
        {
            if (group.Count == 0) return;
            if (showHeaders) sheetOptions.Add(new ActionSheetOption(-1, Loc[headerKey], IsHeader: true));
            foreach (var a in group)
            {
                sheetOptions.Add(new ActionSheetOption(candidates.Count, $"{a.Name} ({a.Cost}gc)"));
                candidates.Add(a);
            }
        }
        AddGroup("WarriorsGroupHeroes", heroArchetypes);
        AddGroup("WarriorsGroupHenchmen", henchmanArchetypes);

        var index = await ShowActionSheetIndexAsync(Loc["WarriorsChooseType"], sheetOptions);
        if (index < 0) return;

        var archetype = candidates[index];
        if (Warband.Treasury < archetype.Cost)
        {
            await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
            return;
        }

        var name = await ShowPromptAsync(Loc["DialogRecruit"], Loc["PromptName"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        await Loading.RunAsync(async () =>
        {
            Warband.Treasury -= archetype.Cost;
            await _warbandService.SaveWarbandAsync(Warband);
            OnPropertyChanged(nameof(Warband));

            var warrior = await _warbandService.RecruitWarriorAsync(Warband.Id, archetype, name);
            var row = ToRow(warrior);
            (archetype.IsHero ? Heroes : Henchmen).Add(row);
        });
    }

    [RelayCommand]
    private async Task EditWarrior(WarriorRow row)
    {
        if (Warband is null) return;

        var w = row.Warrior;
        var copy = new Warrior
        {
            Id = w.Id,
            WarbandId = w.WarbandId,
            WarriorArchetypeId = w.WarriorArchetypeId,
            Name = w.Name,
            IsHero = w.IsHero,
            Cost = w.Cost,
            Experience = w.Experience,
            Status = w.Status,
            HeadCount = w.HeadCount,
            Movement = w.Movement,
            MovementOverride = w.MovementOverride,
            WeaponSkill = w.WeaponSkill,
            BallisticSkill = w.BallisticSkill,
            Strength = w.Strength,
            Toughness = w.Toughness,
            Wounds = w.Wounds,
            Initiative = w.Initiative,
            Attacks = w.Attacks,
            Leadership = w.Leadership,
            EquipmentListId = w.EquipmentListId,
            Equipment = w.Equipment,
            Skills = w.Skills,
            Injuries = w.Injuries,
            Spells = w.Spells,
            Mutations = w.Mutations,
            Animal = w.Animal
        };

        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == w.WarriorArchetypeId);
        var isSpellcaster = archetype?.IsSpellcaster ?? false;
        var isMutant = archetype?.CanBuyMutations ?? false;
        var dialogViewModel = new WarriorEditDialogViewModel(copy, Loc["WarriorEditTitle"], Warband, _warbandService,
            _libraryService, _detailDialogs, _equipmentPicker, _skillPicker, _injuryPicker, _spellPicker, isSpellcaster, _bandMagicSchools,
            _mutationPicker, isMutant, _animalPicker);
        var saved = await ShowDialogAsync(new WarriorEditDialog(dialogViewModel));

        // Toujours recharger, même si le dialog a été annulé : l'ajout/retrait de blessure suivie
        // (AddInjury/RemoveInjury) persiste immédiatement dans le dialog, indépendamment du bouton
        // Enregistrer/Annuler (même logique que l'Équipement/les Compétences sur la carte guerrier).
        await Loading.RunAsync(async () =>
        {
            // WasDeleted : le guerrier n'existe plus en base (supprimé depuis le dialog) - le
            // ré-enregistrer écraserait rien puisqu'il n'y a plus de ligne à mettre à jour. Rembourse
            // son coût de recrutement + tout son équipement (row.Equipment, pas copy.Equipment qui
            // n'est jamais rempli dans ce dialog) ; pas les compétences/blessures, qui n'ont pas de coût.
            if (dialogViewModel.WasDeleted)
            {
                var refund = copy.Cost + row.Equipment.Sum(e => e.Item.Cost * e.Quantity);
                Warband.Treasury += refund;
                await _warbandService.SaveWarbandAsync(Warband);
            }
            else if (saved == true)
            {
                await _warbandService.SaveWarriorAsync(copy);
            }

            await LoadAsync(Warband.Id);
        });
    }

    /// <summary>Grossit l'effectif vivant d'un groupe d'Hommes de main (Warrior.HeadCount) - pas de
    /// notion de Status.Dead pour un groupe, juste un compteur (livre des règles : XP/équipement/
    /// compétences restent partagés par tous les survivants).</summary>
    [RelayCommand]
    private async Task IncrementHeadCount(WarriorRow row)
    {
        row.Warrior.HeadCount++;
        row.RefreshHeadCountDisplay();
        await _warbandService.SaveWarriorAsync(row.Warrior);
    }

    /// <summary>Réduit l'effectif d'un groupe d'Hommes de main - supprime la ligne quand il atteint 0
    /// plutôt que de garder une ligne à 0 sans rien à afficher.</summary>
    [RelayCommand]
    private async Task DecrementHeadCount(WarriorRow row)
    {
        if (row.Warrior.HeadCount <= 1)
        {
            await Loading.RunAsync(async () =>
            {
                await _warbandService.DeleteWarriorAsync(row.Warrior.Id);
                Henchmen.Remove(row);
            });
            return;
        }

        row.Warrior.HeadCount--;
        row.RefreshHeadCountDisplay();
        await _warbandService.SaveWarriorAsync(row.Warrior);
    }

    // Puces de la carte guerrier (Règles spéciales/Blessures/Sorts/Mutations/Monture/Équipement/
    // Compétences) tapables - ouvrent le même dialog récap en lecture seule que la Bibliothèque/le
    // recrutement, via IDetailDialogService (voir ce service pour pourquoi il existe : cette
    // résolution de restrictions était dupliquée à la main dans ~28 endroits avant lui).
    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule);

    [RelayCommand]
    private Task ShowInjuryDetail(WarriorInjury injury) => _detailDialogs.ShowInjuryDetailDialogAsync(injury.Item);

    [RelayCommand]
    private Task ShowSpellDetail(WarriorSpell spell) => _detailDialogs.ShowSpellDetailDialogAsync(spell.Item);

    [RelayCommand]
    private Task ShowMutationDetail(WarriorMutation mutation) => _detailDialogs.ShowMutationDetailDialogAsync(mutation.Item);

    [RelayCommand]
    private Task ShowAnimalDetail(Animal animal) => _detailDialogs.ShowAnimalDetailDialogAsync(animal);

    [RelayCommand]
    private Task ShowEquipmentDetail(WarriorEquipment equipment) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(equipment.Item, equipment.MaterialRule);

    [RelayCommand]
    private Task ShowSkillDetail(WarriorSkill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill.Item);

    /// <summary>École de magie de la bande (puce band-wide, pas liée à un guerrier précis) - même popup
    /// Nom+Description+Sorts que WarbandArchetypeDetailDialogViewModel.ShowMagicSchoolDetail, pas de
    /// XxxDetailDialog dédié pour MagicSchool dans IDetailDialogService (voir ChipDetailDialogViewModel).</summary>
    [RelayCommand]
    private async Task ShowMagicSchoolDetail(MagicSchool school)
    {
        var language = LocalizationService.Instance.Language;
        var spells = (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == school.Id).ToList();
        await ShowDialogAsync(new ChipDetailDialog(new ChipDetailDialogViewModel(school.Name, school.Description, spells)));
    }

    [RelayCommand]
    private async Task EndOfGame()
    {
        if (Warband is null) return;

        var activeWarriors = Heroes.Concat(Henchmen)
            .Select(r => r.Warrior)
            .Where(w => w.Status == WarriorStatus.Active)
            .ToList();
        if (activeWarriors.Count == 0)
        {
            await ShowInfoAsync(Loc["EndOfGameTitle"], Loc["EndOfGameNoWarriors"]);
            return;
        }

        var dialogViewModel = new EndOfGameDialogViewModel(activeWarriors, _skillPicker, _detailDialogs, Warband.WarbandArchetypeId);
        if (await ShowDialogAsync(new EndOfGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            var sentences = new List<string> { string.Format(Loc["HistoryResultSentence"], dialogViewModel.SelectedResult) };

            if (dialogViewModel.TreasuryFound != 0)
            {
                Warband.Treasury += dialogViewModel.TreasuryFound;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryTreasurySentence"], dialogViewModel.TreasuryFound));
            }

            List<Injury>? injuryCatalog = null;

            // Find-or-create par nom (résolu dans la langue courante, comme le catalogue lui-même) dans
            // le catalogue Injury - la table Blessures Graves a un texte fixe par jet, donc pas de
            // risque de quasi-doublons.
            var language = LocalizationService.Instance.Language;
            async Task<Injury> GetOrCreateInjuryAsync(string name)
            {
                injuryCatalog ??= await _libraryService.GetInjuriesAsync(language);
                var injury = injuryCatalog.FirstOrDefault(i => i.Name == name);
                if (injury is null)
                {
                    injury = new Injury { Name = name, Source = ContentSource.Official };
                    await _libraryService.SaveInjuryAsync(injury, language);
                    injuryCatalog.Add(injury);
                }
                return injury;
            }

            foreach (var row in dialogViewModel.WarriorRows)
            {
                var warrior = row.Warrior;
                var changed = false;

                if (row.ExperienceGained != 0)
                {
                    warrior.Experience += row.ExperienceGained;
                    sentences.Add(string.Format(Loc["HistoryXpSentence"], warrior.Name, row.ExperienceGained));
                    changed = true;
                }

                foreach (var advance in row.AdvanceRolls)
                {
                    if (string.IsNullOrWhiteSpace(advance.ResultText)) continue;

                    // Aucun résultat d'Advance (compétence ou stat) ne touche Injuries - ça prêterait à
                    // confusion avec une vraie blessure. La vraie compétence choisie est rattachée au
                    // guerrier ; les résultats de stat/choix (pas d'équivalent structuré dans le modèle,
                    // "no rules engine V1") ne vivent que dans l'Historique de la bande, à appliquer à la
                    // main via l'édition du guerrier.
                    var text = advance.SelectedSkills.Count > 0
                        ? string.Format(Loc["EndOfGameAdvanceSkillResultText"], advance.SelectedSkillsText)
                        : advance.ResultText;
                    sentences.Add(string.Format(Loc["HistoryAdvanceSentence"], warrior.Name, text));

                    foreach (var skill in advance.SelectedSkills)
                        await _warbandService.AddWarriorSkillAsync(warrior.Id, skill);
                }

                if (row.Status != warrior.Status)
                {
                    warrior.Status = row.Status;
                    changed = true;
                    if (warrior.Status == WarriorStatus.Dead)
                        sentences.Add(string.Format(Loc["HistoryDeathSentence"], warrior.Name));
                }

                if (!string.IsNullOrWhiteSpace(row.InjuryResultText))
                {
                    var injury = await GetOrCreateInjuryAsync(row.InjuryResultText);
                    await _warbandService.AddWarriorInjuryAsync(warrior.Id, injury);
                    sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, row.InjuryResultText));
                }

                if (changed)
                    await _warbandService.SaveWarriorAsync(warrior);
            }

            await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));
            await LoadAsync(Warband.Id);
        });
    }

    [RelayCommand]
    private async Task AddNote()
    {
        if (Warband is null) return;

        var text = await ShowPromptAsync(Loc["HistoryNotePromptTitle"], Loc["PromptName"]);
        if (string.IsNullOrWhiteSpace(text)) return;

        await _warbandService.AddHistoryEntryAsync(Warband.Id, text);
        var history = await _warbandService.GetHistoryEntriesAsync(Warband.Id);
        HistoryEntries = new ObservableCollection<HistoryEntry>(history);
    }

}
