using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly IInjuryPickerService _injuryPicker;

    private List<WarriorArchetype> _recruitableArchetypes = new();
    private Dictionary<int, string> _archetypeNames = new();

    [ObservableProperty]
    private int warbandId;

    [ObservableProperty]
    private Warband? warband;

    [ObservableProperty]
    private ObservableCollection<WarriorRow> heroes = new();

    [ObservableProperty]
    private ObservableCollection<WarriorRow> henchmen = new();

    [ObservableProperty]
    private bool heroesExpanded = true;

    [ObservableProperty]
    private bool henchmenExpanded = true;

    // IsSelected porté par la ligne (SelectionMode="None" sur le CollectionView), pas la sélection
    // native : constaté à l'usage (screenshot Android) que même un Border stylé via
    // VisualStateManager (SelectableGridItemBorderStyle, utilisé par la Library) n'empêche pas
    // Android d'afficher son propre fond de sélection teinté colorAccent par-dessus - seule une
    // sélection entièrement gérée à la main l'évite, même mécanisme que WarbandRow/WarbandListPage.
    [ObservableProperty]
    private WarriorRow? selectedRow;

    [ObservableProperty]
    private ObservableCollection<HistoryEntry> historyEntries = new();

    [ObservableProperty]
    private bool showHistory;

    public WarbandDetailViewModel(IWarbandService warbandService, ILibraryService libraryService,
        IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker, IInjuryPickerService injuryPicker)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _injuryPicker = injuryPicker;
    }

    partial void OnWarbandIdChanged(int value) => _ = LoadAsync(value);

    partial void OnSelectedRowChanged(WarriorRow? oldValue, WarriorRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(WarriorRow row) => SelectedRow = row;

    [RelayCommand]
    private void ToggleHeroes() => HeroesExpanded = !HeroesExpanded;

    [RelayCommand]
    private void ToggleHenchmen() => HenchmenExpanded = !HenchmenExpanded;

    private async Task LoadAsync(int id)
    {
        await Loading.RunAsync(async () =>
        {
            Warband = await _warbandService.GetWarbandAsync(id);
            if (Warband is null) return;

            _recruitableArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId);
            _archetypeNames = _recruitableArchetypes.ToDictionary(a => a.Id, a => a.Name);

            var loaded = await _warbandService.GetWarriorsAsync(id);
            var rows = loaded.Select(ToRow).ToList();
            Heroes = new ObservableCollection<WarriorRow>(rows.Where(r => r.Warrior.IsHero));
            Henchmen = new ObservableCollection<WarriorRow>(rows.Where(r => !r.Warrior.IsHero));
            SelectedRow = null;

            var history = await _warbandService.GetHistoryEntriesAsync(id);
            HistoryEntries = new ObservableCollection<HistoryEntry>(history);
        });
    }

    private WarriorRow ToRow(Warrior warrior) =>
        new(warrior, _archetypeNames.GetValueOrDefault(warrior.WarriorArchetypeId, "?"));

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

        var name = await ShowPromptAsync(Loc["DialogRecruit"], Loc["PromptName"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        await Loading.RunAsync(async () =>
        {
            var archetype = candidates[index];
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
            Movement = w.Movement,
            WeaponSkill = w.WeaponSkill,
            BallisticSkill = w.BallisticSkill,
            Strength = w.Strength,
            Toughness = w.Toughness,
            Wounds = w.Wounds,
            Initiative = w.Initiative,
            Attacks = w.Attacks,
            Leadership = w.Leadership,
            Injuries = w.Injuries
        };

        var dialogViewModel = new WarriorEditDialogViewModel(copy, Loc["WarriorEditTitle"], _warbandService, _injuryPicker);
        var saved = await ShowDialogAsync(new WarriorEditDialog(dialogViewModel));

        // Toujours recharger, même si le dialog a été annulé : l'ajout/retrait de blessure suivie
        // (AddInjury/RemoveInjury) persiste immédiatement dans le dialog, indépendamment du bouton
        // Enregistrer/Annuler (même logique que l'Équipement/les Compétences sur la carte guerrier).
        await Loading.RunAsync(async () =>
        {
            if (saved == true)
                await _warbandService.SaveWarriorAsync(copy);

            await LoadAsync(Warband.Id);
        });
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

        var dialogViewModel = new EndOfGameDialogViewModel(activeWarriors);
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

                if (row.Status != warrior.Status)
                {
                    warrior.Status = row.Status;
                    changed = true;
                    if (warrior.Status == WarriorStatus.Dead)
                        sentences.Add(string.Format(Loc["HistoryDeathSentence"], warrior.Name));
                }

                if (!string.IsNullOrWhiteSpace(row.InjuryResultText))
                {
                    // Même liste que celle suivie manuellement (WarriorEditDialog) : find-or-create par
                    // nom plutôt qu'un doublon en texte libre - la table Blessures Graves a un texte
                    // fixe par jet, donc pas de risque de quasi-doublons.
                    injuryCatalog ??= await _libraryService.GetInjuriesAsync();
                    var injury = injuryCatalog.FirstOrDefault(i => i.Name == row.InjuryResultText);
                    if (injury is null)
                    {
                        injury = new Injury { Name = row.InjuryResultText, Source = ContentSource.Official };
                        await _libraryService.SaveInjuryAsync(injury);
                        injuryCatalog.Add(injury);
                    }

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

    [RelayCommand]
    private async Task AddEquipment(WarriorRow row)
    {
        var items = await _equipmentPicker.PickEquipmentAsync();
        foreach (var item in items)
        {
            var carried = await _warbandService.AddWarriorEquipmentAsync(row.Warrior.Id, item);
            row.Equipment.Add(carried);
        }
    }

    [RelayCommand]
    private async Task RemoveEquipment(WarriorEquipment carried)
    {
        var row = Heroes.Concat(Henchmen).FirstOrDefault(r => r.Equipment.Contains(carried));
        if (row is null) return;

        await _warbandService.RemoveWarriorEquipmentAsync(carried.Id);
        row.Equipment.Remove(carried);
    }

    [RelayCommand]
    private async Task AddSkill(WarriorRow row)
    {
        var skills = await _skillPicker.PickSkillAsync();
        foreach (var skill in skills)
        {
            var learned = await _warbandService.AddWarriorSkillAsync(row.Warrior.Id, skill);
            row.Skills.Add(learned);
        }
    }

    [RelayCommand]
    private async Task RemoveSkill(WarriorSkill learned)
    {
        var row = Heroes.Concat(Henchmen).FirstOrDefault(r => r.Skills.Contains(learned));
        if (row is null) return;

        await _warbandService.RemoveWarriorSkillAsync(learned.Id);
        row.Skills.Remove(learned);
    }
}
