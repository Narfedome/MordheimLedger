using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit
{
    /// <summary>2 étapes (Général/Guerriers) : Règles/Magie/Équipement n'ont pas de sens ici - Item est
    /// un Warband (l'instance jouée), qui n'a pas sa propre copie de ces catalogues, elle référence ceux
    /// de son WarbandArchetype (consultables via ShowArchetypeDetail/le Codex). Général = Nom + choix de
    /// l'Archetype (ChipItemView, sélection unique obligatoire) ; Guerriers = recrutement de vrais
    /// Warrior en mémoire, voir Warriors/AddWarrior - rien n'est persisté avant Save.</summary>
    public partial class WarbandEditDialogViewModel : DialogViewModel<bool>
    {
        private const int StepCount = 2;

        private readonly IWarbandArchetypePickerService _warbandArchetypePicker;
        private readonly IWarbandService _warbandService;
        private readonly ILibraryService _libraryService;
        private List<WarriorArchetype> _recruitableArchetypes = new();
        private bool _recruitableLoaded;

        public bool IsWizardMode { get; }
        protected override bool CancelResult => false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RosterCountDisplay))]
        [NotifyPropertyChangedFor(nameof(RemainingTreasuryDisplay))]
        private WarbandArchetype? archetype;

        [ObservableProperty]
        private Warband item;

        [ObservableProperty]
        private string? nameError;

        [ObservableProperty]
        private string? archetypeError;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
        [NotifyPropertyChangedFor(nameof(IsWarriorsTab))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyPropertyChangedFor(nameof(IsLastStep))]
        [NotifyPropertyChangedFor(nameof(StepLabel))]
        private int selectedTab;
        public bool IsGeneralTab => SelectedTab == 0;
        public bool IsWarriorsTab => SelectedTab == 1;

        /// <summary>Mode assistant (IsWizardMode) uniquement : pilote Précédent/le libellé d'étape.</summary>
        public bool CanGoBack => SelectedTab > 0;
        public bool IsLastStep => SelectedTab == StepCount - 1;
        public string StepLabel => string.Format(Loc["LibStepLabel"], SelectedTab + 1, StepCount);

        /// <summary>Recrues en mémoire uniquement - rien n'est persisté avant Save, même principe différé
        /// que WarbandArchetypeEditDialogViewModel.Warriors/EquipmentLists.</summary>
        [ObservableProperty]
        private ObservableCollection<Warrior> warriors = new();

        public string RosterCountDisplay
        {
            get
            {
                if (Archetype is null) return string.Empty;
                var countText = Archetype.MaxWarriors is { } max ? $"{Warriors.Count}/{max}" : Warriors.Count.ToString();
                if (Archetype.MinWarriors is { } min) countText += $" ({string.Format(Loc["WarbandsRosterMinSuffix"], min)})";
                return countText;
            }
        }

        public string RemainingTreasuryDisplay
        {
            get
            {
                if (Archetype is null) return string.Empty;
                return string.Format(Loc["WarbandsRosterTreasuryRemaining"], Archetype.StartingTreasury - Warriors.Sum(w => w.Cost));
            }
        }

        [ObservableProperty]
        private string? warriorsError;

        public WarbandEditDialogViewModel(Warband item, string title, IWarbandArchetypePickerService warbandArchetypePicker,
            IWarbandService warbandService, ILibraryService libraryService)
        {
            this.item = item;
            this.title = title;
            _warbandArchetypePicker = warbandArchetypePicker;
            _warbandService = warbandService;
            _libraryService = libraryService;
            IsWizardMode = item.Id == 0;
        }

        [RelayCommand]
        private void ShowGeneralTab() => SelectedTab = 0;

        [RelayCommand]
        private async Task ShowWarriorsTab()
        {
            SelectedTab = 1;
            await EnsureRecruitableArchetypesLoadedAsync();
        }

        [RelayCommand]
        private async Task AddArchetype()
        {
            var picked = await _warbandArchetypePicker.PickWarbandArchetypeAsync();
            if (picked is null || picked.Id == 0) return;

            Archetype = picked;
            ArchetypeError = null;

            // Change d'archetype en cours de création : les recrues déjà choisies venaient du catalogue
            // de l'ancien archetype, plus valides pour le nouveau.
            _recruitableLoaded = false;
            Warriors.Clear();
        }

        [RelayCommand]
        private void RemoveArchetype()
        {
            Archetype = null;
            _recruitableLoaded = false;
            Warriors.Clear();
        }

        [RelayCommand]
        private async Task ShowArchetypeDetail(WarbandArchetype _)
        {
            if (Archetype is null) return;
            var language = LocalizationService.Instance.Language;
            WarbandArchetype? fullWarband = null;
            await Loading.RunAsync(async () =>
            {
                fullWarband = await Task.Run(() => _libraryService.GetWarbandArchetypeAsync(Archetype.Id, language));
            });
            if (fullWarband is null) return;

            await ShowDialogAsync(new WarbandArchetypeDetailDialog(new WarbandArchetypeDetailDialogViewModel(fullWarband, _libraryService)));
        }

        private async Task EnsureRecruitableArchetypesLoadedAsync()
        {
            if (_recruitableLoaded || Archetype is null) return;
            var language = LocalizationService.Instance.Language;
            await Loading.RunAsync(async () =>
            {
                _recruitableArchetypes = await Task.Run(() => _libraryService.GetWarriorArchetypesAsync(Archetype.Id, language));
            });
            _recruitableLoaded = true;
        }

        [RelayCommand]
        private async Task AddWarrior()
        {
            if (Archetype is null) return;
            await EnsureRecruitableArchetypesLoadedAsync();

            if (Archetype.MaxWarriors is { } maxWarriors && Warriors.Count >= maxWarriors)
            {
                await ShowInfoAsync(Loc["WarbandsRosterFullTitle"], Loc["WarbandsRosterFullMessage"]);
                return;
            }

            var countByArchetypeId = Warriors.GroupBy(w => w.WarriorArchetypeId).ToDictionary(g => g.Key, g => g.Count());
            var remainingTreasury = Archetype.StartingTreasury - Warriors.Sum(w => w.Cost);

            // Types déjà à leur MaxCount ou trop chers pour la trésorerie restante : exclus de la liste
            // proposée plutôt que sélectionnables puis rejetés après coup.
            var candidates = _recruitableArchetypes
                .Where(a => a.MaxCount is not { } max || countByArchetypeId.GetValueOrDefault(a.Id) < max)
                .Where(a => a.Cost <= remainingTreasury)
                .ToList();

            if (candidates.Count == 0)
            {
                await ShowInfoAsync(Loc["WarriorsEmptyLibraryTitle"], Loc["WarbandsNoRecruitableWarriorsMessage"]);
                return;
            }

            var heroArchetypes = candidates.Where(a => a.IsHero).ToList();
            var henchmanArchetypes = candidates.Where(a => !a.IsHero).ToList();
            var showHeaders = heroArchetypes.Count > 0 && henchmanArchetypes.Count > 0;

            var pool = new List<WarriorArchetype>();
            var sheetOptions = new List<ActionSheetOption>();
            void AddGroup(string headerKey, List<WarriorArchetype> group)
            {
                if (group.Count == 0) return;
                if (showHeaders) sheetOptions.Add(new ActionSheetOption(-1, Loc[headerKey], IsHeader: true));
                foreach (var a in group)
                {
                    sheetOptions.Add(new ActionSheetOption(pool.Count, $"{a.Name} ({a.Cost}gc)"));
                    pool.Add(a);
                }
            }
            AddGroup("WarriorsGroupHeroes", heroArchetypes);
            AddGroup("WarriorsGroupHenchmen", henchmanArchetypes);

            var index = await ShowActionSheetIndexAsync(Loc["WarriorsChooseType"], sheetOptions);
            if (index < 0) return;

            var archetype = pool[index];
            var name = await ShowPromptAsync(Loc["DialogRecruit"], Loc["PromptName"]);
            if (string.IsNullOrWhiteSpace(name)) return;

            Warriors.Add(archetype.ToWarrior(name));
            OnPropertyChanged(nameof(RosterCountDisplay));
            OnPropertyChanged(nameof(RemainingTreasuryDisplay));
        }

        [RelayCommand]
        private void RemoveWarrior(Warrior warrior)
        {
            Warriors.Remove(warrior);
            OnPropertyChanged(nameof(RosterCountDisplay));
            OnPropertyChanged(nameof(RemainingTreasuryDisplay));
        }

        /// <summary>Onglet Général : Nom et Archetype obligatoires. Pose NameError/ArchetypeError (texte
        /// affiché sous le champ, pas juste une couleur) si invalide.</summary>
        private bool ValidateGeneralStep()
        {
            NameError = string.IsNullOrWhiteSpace(Item.Name) ? Loc["LibFieldRequired"] : null;
            ArchetypeError = Archetype is null ? Loc["LibFieldRequired"] : null;
            return NameError is null && ArchetypeError is null;
        }

        /// <summary>Onglet Guerriers : effectif minimum de la bande, et présence au bon nombre de chaque
        /// type "obligatoire" (MinCount &gt; 0, ex. le meneur unique d'une bande) - voir
        /// WarriorArchetype.MinCount. Nécessite _recruitableArchetypes chargé (voir Save).</summary>
        private bool ValidateWarriorsStep()
        {
            if (Archetype is null) return false;

            if (Warriors.Count < (Archetype.MinWarriors ?? 0))
            {
                WarriorsError = string.Format(Loc["WarbandsMinWarriorsError"], Archetype.MinWarriors);
                return false;
            }

            var countByArchetypeId = Warriors.GroupBy(w => w.WarriorArchetypeId).ToDictionary(g => g.Key, g => g.Count());
            foreach (var required in _recruitableArchetypes.Where(a => a.MinCount is > 0))
            {
                if (countByArchetypeId.GetValueOrDefault(required.Id) < required.MinCount)
                {
                    WarriorsError = string.Format(Loc["WarbandsRequiredWarriorMissing"], required.Name, required.MinCount);
                    return false;
                }
            }

            WarriorsError = null;
            return true;
        }

        /// <summary>Mode assistant uniquement (bouton Suivant) - avance d'une étape, Général→Guerriers.
        /// Validé par étape : en quittant Général, bloque tant que Nom/Archetype ne sont pas renseignés.</summary>
        [RelayCommand]
        private async Task Next()
        {
            if (IsGeneralTab && !ValidateGeneralStep()) return;
            if (SelectedTab >= StepCount - 1) return;
            SelectedTab++;

            if (IsWarriorsTab) await EnsureRecruitableArchetypesLoadedAsync();
        }

        /// <summary>Mode assistant uniquement (bouton Précédent).</summary>
        [RelayCommand]
        private void Back()
        {
            if (SelectedTab > 0) SelectedTab--;
        }

        /// <summary>Seul point d'écriture en base de tout le dialog - la bande d'abord (INSERT via
        /// CreateWarbandAsync si Item.Id == 0, sinon UPDATE via SaveWarbandAsync), puis chaque recrue en
        /// attente. Item.Id == 0 est le seul cas atteignable aujourd'hui (WarbandListViewModel n'ouvre ce
        /// dialog qu'en création) - la branche Update est gardée correcte si un futur appelant réutilise
        /// ce dialog pour éditer une bande existante.</summary>
        [RelayCommand]
        private async Task Save()
        {
            if (!ValidateGeneralStep())
            {
                SelectedTab = 0;
                return;
            }

            await EnsureRecruitableArchetypesLoadedAsync();
            if (!ValidateWarriorsStep())
            {
                SelectedTab = 1;
                return;
            }

            await Loading.RunAsync(async () =>
            {
                int warbandId;
                if (Item.Id == 0)
                {
                    var created = await _warbandService.CreateWarbandAsync(Item.Name, Archetype!);
                    warbandId = created.Id;
                    created.Treasury = Archetype!.StartingTreasury - Warriors.Sum(w => w.Cost);
                    await _warbandService.SaveWarbandAsync(created);
                }
                else
                {
                    warbandId = Item.Id;
                    await _warbandService.SaveWarbandAsync(Item);
                }

                foreach (var warrior in Warriors)
                {
                    var recruitedArchetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == warrior.WarriorArchetypeId);
                    if (recruitedArchetype is null) continue;
                    await _warbandService.RecruitWarriorAsync(warbandId, recruitedArchetype, warrior.Name);
                }
            });

            Close(true);
        }
    }
}
