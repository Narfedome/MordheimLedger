using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit
{
    /// <summary>2 étapes (Général/Guerriers) : Règles/Magie/Équipement n'ont pas de sens ici - Item est
    /// un Warband (l'instance jouée), qui n'a pas sa propre copie de ces catalogues, elle référence ceux
    /// de son WarbandArchetype (consultables via ShowArchetypeDetail/le Codex). Général = Nom + choix de
    /// l'Archetype (ChipItemView, sélection unique obligatoire) ; Guerriers = un WarriorRecruitRow par
    /// type recrutable (WarriorRecruitListView : compteur 0/MaxCount + une Entry de nom par recrue
    /// Héros), aplati en vrais Warrior seulement au Save - rien n'est persisté avant.</summary>
    public partial class WarbandEditDialogViewModel : DialogViewModel<bool>
    {
        private const int StepCount = 2;

        private readonly IWarbandArchetypePickerService _warbandArchetypePicker;
        private readonly IWarbandService _warbandService;
        private readonly ILibraryService _libraryService;
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

        /// <summary>Une ligne par WarriorArchetype recrutable pour l'Archetype choisi - voir
        /// WarriorRecruitRow. Peuplée une fois par EnsureRecruitableArchetypesLoadedAsync, vidée/
        /// rechargée si l'Archetype change (AddArchetype/RemoveArchetype).</summary>
        [ObservableProperty]
        private ObservableCollection<WarriorRecruitRow> recruitRows = new();

        public string RosterCountDisplay
        {
            get
            {
                if (Archetype is null) return string.Empty;
                var total = RecruitRows.Sum(r => r.Count);
                var countText = Archetype.MaxWarriors is { } max ? $"{total}/{max}" : total.ToString();
                if (Archetype.MinWarriors is { } min) countText += $" ({string.Format(Loc["WarbandsRosterMinSuffix"], min)})";
                return countText;
            }
        }

        public string RemainingTreasuryDisplay
        {
            get
            {
                if (Archetype is null) return string.Empty;
                return string.Format(Loc["WarbandsRosterTreasuryRemaining"], RemainingTreasury);
            }
        }

        private int RemainingTreasury => (Archetype?.StartingTreasury ?? 0) - RecruitRows.Sum(r => r.Count * r.Cost);

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
            RecruitRows.Clear();
        }

        [RelayCommand]
        private void RemoveArchetype()
        {
            Archetype = null;
            _recruitableLoaded = false;
            RecruitRows.Clear();
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
            List<WarriorArchetype> loaded = new();
            await Loading.RunAsync(async () =>
            {
                loaded = await Task.Run(() => _libraryService.GetWarriorArchetypesAsync(Archetype.Id, language));
            });
            RecruitRows = new ObservableCollection<WarriorRecruitRow>(loaded.Select(a => new WarriorRecruitRow(a)));
            _recruitableLoaded = true;
            UpdateRecruitability();
        }

        [RelayCommand]
        private async Task ShowWarriorDetail(WarriorRecruitRow row)
        {
            var language = LocalizationService.Instance.Language;
            WarriorArchetype? fullWarrior = null;
            await Loading.RunAsync(async () =>
            {
                fullWarrior = await Task.Run(() => _libraryService.GetWarriorArchetypeAsync(row.Archetype.Id, language));
            });
            if (fullWarrior is null) return;

            // Pas de listes d'équipement chargées à ce stade (pas d'étape Équipement pour l'instant) -
            // EquipmentListDisplay retombe sur "aucune" dans le dialog récap, comme pour un guerrier
            // sans EquipmentListId.
            await ShowDialogAsync(new WarriorArchetypeDetailDialog(new WarriorArchetypeDetailDialogViewModel(fullWarrior, Array.Empty<NamedRef>())));
        }

        /// <summary>Un guerrier de plus de ce type : bloqué si son MaxCount, l'effectif max de la bande ou
        /// la trésorerie restante ne le permet plus (IncrementCommand.CanIncrement reflète déjà ça sur
        /// le bouton, ce garde-fou couvre le cas où l'état a changé entre-temps). Héros : ajoute un slot
        /// de nom vide, à renseigner avant de pouvoir Enregistrer (voir ValidateWarriorsStep).</summary>
        [RelayCommand]
        private void IncrementWarrior(WarriorRecruitRow row)
        {
            if (Archetype is null) return;
            if (row.Archetype.MaxCount is { } max && row.Count >= max) return;
            if (Archetype.MaxWarriors is { } maxWarriors && RecruitRows.Sum(r => r.Count) >= maxWarriors) return;
            if (RemainingTreasury < row.Cost) return;

            row.Count++;
            if (row.IsHero) row.NameSlots.Add(new WarriorNameSlot());
            UpdateRecruitability();
        }

        [RelayCommand]
        private void DecrementWarrior(WarriorRecruitRow row)
        {
            if (row.Count == 0) return;
            row.Count--;
            if (row.IsHero && row.NameSlots.Count > 0) row.NameSlots.RemoveAt(row.NameSlots.Count - 1);
            UpdateRecruitability();
        }

        /// <summary>Recalcule les affichages récap (effectif/trésorerie) et CanIncrement de chaque ligne -
        /// appelé après tout changement de compteur, car le MaxWarriors de la bande et la trésorerie
        /// restante dépendent de TOUTES les lignes, pas seulement celle qui vient de changer.</summary>
        private void UpdateRecruitability()
        {
            OnPropertyChanged(nameof(RosterCountDisplay));
            OnPropertyChanged(nameof(RemainingTreasuryDisplay));

            if (Archetype is null) return;
            var total = RecruitRows.Sum(r => r.Count);
            var rosterFull = Archetype.MaxWarriors is { } maxWarriors && total >= maxWarriors;
            foreach (var row in RecruitRows)
            {
                var atMaxCount = row.Archetype.MaxCount is { } max && row.Count >= max;
                row.CanIncrement = !atMaxCount && !rosterFull && RemainingTreasury >= row.Cost;
            }
        }

        /// <summary>Onglet Général : Nom et Archetype obligatoires. Pose NameError/ArchetypeError (texte
        /// affiché sous le champ, pas juste une couleur) si invalide.</summary>
        private bool ValidateGeneralStep()
        {
            NameError = string.IsNullOrWhiteSpace(Item.Name) ? Loc["LibFieldRequired"] : null;
            ArchetypeError = Archetype is null ? Loc["LibFieldRequired"] : null;
            return NameError is null && ArchetypeError is null;
        }

        /// <summary>Onglet Guerriers : effectif minimum de la bande, présence au bon nombre de chaque type
        /// "obligatoire" (MinCount &gt; 0, ex. le meneur unique d'une bande - voir WarriorArchetype.
        /// MinCount), et un nom renseigné pour chaque recrue Héros.</summary>
        private bool ValidateWarriorsStep()
        {
            if (Archetype is null) return false;

            var total = RecruitRows.Sum(r => r.Count);
            if (total < (Archetype.MinWarriors ?? 0))
            {
                WarriorsError = string.Format(Loc["WarbandsMinWarriorsError"], Archetype.MinWarriors);
                return false;
            }

            foreach (var row in RecruitRows.Where(r => r.Archetype.MinCount is > 0))
            {
                if (row.Count < row.Archetype.MinCount)
                {
                    WarriorsError = string.Format(Loc["WarbandsRequiredWarriorMissing"], row.Name, row.Archetype.MinCount);
                    return false;
                }
            }

            foreach (var row in RecruitRows.Where(r => r.IsHero))
            {
                if (row.NameSlots.Any(slot => string.IsNullOrWhiteSpace(slot.Name)))
                {
                    WarriorsError = string.Format(Loc["WarbandsWarriorNameRequired"], row.Name);
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
        /// attente (RecruitRows aplaties en Warrior ici, pas avant). Item.Id == 0 est le seul cas
        /// atteignable aujourd'hui (WarbandListViewModel n'ouvre ce dialog qu'en création) - la branche
        /// Update est gardée correcte si un futur appelant réutilise ce dialog pour éditer une bande
        /// existante.</summary>
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
                var totalCost = RecruitRows.Sum(r => r.Count * r.Cost);
                if (Item.Id == 0)
                {
                    var created = await _warbandService.CreateWarbandAsync(Item.Name, Archetype!);
                    warbandId = created.Id;
                    created.Treasury = Archetype!.StartingTreasury - totalCost;
                    await _warbandService.SaveWarbandAsync(created);
                }
                else
                {
                    warbandId = Item.Id;
                    await _warbandService.SaveWarbandAsync(Item);
                }

                foreach (var row in RecruitRows.Where(r => r.Count > 0))
                {
                    if (row.IsHero)
                    {
                        foreach (var slot in row.NameSlots)
                            await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, slot.Name.Trim());
                    }
                    else
                    {
                        for (var i = 0; i < row.Count; i++)
                            await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, row.Archetype.Name);
                    }
                }
            });

            Close(true);
        }
    }
}
