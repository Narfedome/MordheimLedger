using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit
{
    /// <summary>3 étapes (Général/Guerriers/Équipement) : Règles/Magie n'ont pas de sens ici - Item est un
    /// Warband (l'instance jouée), qui n'a pas sa propre copie de ces catalogues, elle référence ceux de
    /// son WarbandArchetype (consultables via ShowArchetypeDetail/le Codex). Général = Nom + choix de
    /// l'Archetype (ChipItemView, sélection unique obligatoire) ; Guerriers = un WarriorRecruitRow par
    /// type recrutable (WarriorRecruitListView : compteur 0/MaxCount + une Entry de nom par recrue
    /// Héros) ; Équipement = achat par groupe pour les Hommes de main (WarriorRecruitRow.GroupEquipment,
    /// même équipement pour tous - livre des règles) et par individu pour les Héros
    /// (WarriorNameSlot.Equipment, chacun peut différer). Tout est aplati en vrais Warrior/
    /// WarriorEquipment seulement au Save - rien n'est persisté avant.</summary>
    public partial class WarbandEditDialogViewModel : DialogViewModel<bool>
    {
        private const int StepCount = 3;

        private readonly IWarbandArchetypePickerService _warbandArchetypePicker;
        private readonly IWarbandService _warbandService;
        private readonly ILibraryService _libraryService;
        private readonly IEquipmentPickerService _equipmentPicker;
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
        [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyPropertyChangedFor(nameof(IsLastStep))]
        [NotifyPropertyChangedFor(nameof(StepLabel))]
        private int selectedTab;
        public bool IsGeneralTab => SelectedTab == 0;
        public bool IsWarriorsTab => SelectedTab == 1;
        public bool IsEquipmentTab => SelectedTab == 2;

        /// <summary>Mode assistant (IsWizardMode) uniquement : pilote Précédent/le libellé d'étape.</summary>
        public bool CanGoBack => SelectedTab > 0;
        public bool IsLastStep => SelectedTab == StepCount - 1;
        public string StepLabel => string.Format(Loc["LibStepLabel"], SelectedTab + 1, StepCount);

        /// <summary>Une ligne par WarriorArchetype recrutable pour l'Archetype choisi - voir
        /// WarriorRecruitRow. Peuplée une fois par EnsureRecruitableArchetypesLoadedAsync, vidée/
        /// rechargée si l'Archetype change (AddArchetype/RemoveArchetype).</summary>
        [ObservableProperty]
        private ObservableCollection<WarriorRecruitRow> recruitRows = new();

        /// <summary>Sous-ensemble de RecruitRows effectivement recruté (Count > 0) - c'est la seule chose
        /// pertinente à afficher à l'étape Équipement, pas la liste complète des types disponibles.</summary>
        public IEnumerable<WarriorRecruitRow> RecruitedRows => RecruitRows.Where(r => r.Count > 0);

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

        /// <summary>Coût de recrutement de chaque ligne + équipement de groupe (Hommes de main, coût ×
        /// effectif) + équipement individuel (chaque slot Héros).</summary>
        private int TotalSpent => RecruitRows.Sum(r => r.Count * r.Cost)
            + RecruitRows.Sum(r => r.GroupEquipment.Sum(e => e.Cost) * r.Count)
            + RecruitRows.SelectMany(r => r.NameSlots).Sum(s => s.Equipment.Sum(e => e.Cost));

        private int RemainingTreasury => (Archetype?.StartingTreasury ?? 0) - TotalSpent;

        [ObservableProperty]
        private string? warriorsError;

        public WarbandEditDialogViewModel(Warband item, string title, IWarbandArchetypePickerService warbandArchetypePicker,
            IWarbandService warbandService, ILibraryService libraryService, IEquipmentPickerService equipmentPicker)
        {
            this.item = item;
            this.title = title;
            _warbandArchetypePicker = warbandArchetypePicker;
            _warbandService = warbandService;
            _libraryService = libraryService;
            _equipmentPicker = equipmentPicker;
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
        private async Task ShowEquipmentTab()
        {
            SelectedTab = 2;
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

            // Pas de listes d'équipement chargées à ce stade (le nom de la liste, pas son contenu, n'est
            // pas utile ici) - EquipmentListDisplay retombe sur "aucune" dans le dialog récap.
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
            if (row.IsHero) row.NameSlots.Add(new WarriorNameSlot(row));
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
            OnPropertyChanged(nameof(RecruitedRows));

            if (Archetype is null) return;
            var total = RecruitRows.Sum(r => r.Count);
            var rosterFull = Archetype.MaxWarriors is { } maxWarriors && total >= maxWarriors;
            foreach (var row in RecruitRows)
            {
                var atMaxCount = row.Archetype.MaxCount is { } max && row.Count >= max;
                row.CanIncrement = !atMaxCount && !rosterFull && RemainingTreasury >= row.Cost;
            }
        }

        /// <summary>Achat d'équipement pour une cible : un WarriorRecruitRow (Homme de main - un seul
        /// achat, appliqué à tout le groupe) ou un WarriorNameSlot (Héros - propre à cette recrue). Même
        /// logique que WarriorEditDialogViewModel.AddEquipment (picker filtré par EquipmentListId/
        /// WarriorArchetypeId, choix de matériau pour les armes de corps à corps, arrêt au premier objet
        /// trop cher) - simplement pas encore de WarriorId réel pour appeler AddWarriorEquipmentAsync,
        /// donc on garde des EquipmentPick en mémoire jusqu'au Save.</summary>
        [RelayCommand]
        private async Task AddEquipment(object target)
        {
            WarriorRecruitRow row;
            ObservableCollection<EquipmentPick> destination;
            int perUnitCost;
            switch (target)
            {
                case WarriorNameSlot slot:
                    row = slot.Row;
                    destination = slot.Equipment;
                    perUnitCost = 1;
                    break;
                case WarriorRecruitRow r:
                    row = r;
                    destination = r.GroupEquipment;
                    perUnitCost = r.Count;
                    break;
                default:
                    return;
            }
            if (Archetype is null) return;

            var items = await _equipmentPicker.PickEquipmentAsync(Archetype.Id, row.Archetype.EquipmentListId, row.Archetype.Id, RemainingTreasury, perUnitCost);
            foreach (var equipmentItem in items)
            {
                SpecialRule? materialRule = null;
                if (equipmentItem.Category == EquipmentCategory.MeleeWeapon)
                {
                    var materialRules = (await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language))
                        .Where(r => r.CostMultiplier.HasValue).ToList();
                    if (materialRules.Count > 0)
                    {
                        var options = new[] { Loc["WarriorsMaterialNormal"] }.Concat(materialRules.Select(r => r.Name)).ToArray();
                        var index = await ShowActionSheetIndexAsync(Loc["WarriorsMaterialPickerTitle"], options);
                        if (index > 0) materialRule = materialRules[index - 1];
                    }
                }

                var pick = new EquipmentPick(equipmentItem, materialRule);

                // Coût total si on achète maintenant (perUnitCost = l'effectif du groupe pour un Homme de
                // main, 1 pour un Héros) - sélection multiple : on s'arrête au premier objet trop cher
                // plutôt que de tout annuler, même logique que WarriorEditDialogViewModel.AddEquipment.
                if (RemainingTreasury < pick.Cost * perUnitCost)
                {
                    await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                    break;
                }

                destination.Add(pick);
            }

            UpdateRecruitability();
        }

        /// <summary>Tap sur un chip d'équipement acheté (groupe ou slot Héros) - même recap qu'ailleurs
        /// dans l'app (EquipmentListEditDialogViewModel.ShowItemDetail, EquipmentItemViewModel.ShowDetails),
        /// pas juste le mini-popup Nom+Description générique (ChipDetailDialog) : un objet d'équipement a
        /// coût/rareté/restrictions propres, pas seulement une description.</summary>
        [RelayCommand]
        private async Task ShowEquipmentDetail(EquipmentPick pick)
        {
            var equipmentItem = pick.Item;
            var language = LocalizationService.Instance.Language;
            var categoryLabel = Loc[$"EquipmentCategory{equipmentItem.Category}"];

            var restrictedWarbands = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0
                ? new List<WarbandArchetype>()
                : (await _libraryService.GetWarbandArchetypesAsync(language))
                    .Where(w => equipmentItem.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

            var restrictedWarriors = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0 || equipmentItem.RestrictedToWarriorArchetypeIds.Count == 0
                ? new List<WarriorArchetype>()
                : (await _libraryService.GetWarriorArchetypesAsync(equipmentItem.RestrictedToWarbandArchetypeIds, language))
                    .Where(w => equipmentItem.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

            await ShowDialogAsync(new EquipmentItemDetailDialog(
                new EquipmentItemDetailDialogViewModel(equipmentItem, categoryLabel, restrictedWarbands, restrictedWarriors, pick.MaterialRule)));
        }

        /// <summary>Retire un EquipmentPick de quelle que collection le contient (GroupEquipment d'une
        /// ligne ou Equipment d'un slot Héros) - identité de référence, pas besoin de savoir d'avance
        /// laquelle puisque chaque instance n'est ajoutée qu'à une seule collection.</summary>
        [RelayCommand]
        private void RemoveEquipment(EquipmentPick pick)
        {
            foreach (var row in RecruitRows)
            {
                if (row.GroupEquipment.Remove(pick))
                {
                    UpdateRecruitability();
                    return;
                }
                foreach (var slot in row.NameSlots)
                {
                    if (slot.Equipment.Remove(pick))
                    {
                        UpdateRecruitability();
                        return;
                    }
                }
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

        /// <summary>Mode assistant uniquement (bouton Suivant) - avance d'une étape, Général→Guerriers→
        /// Équipement. Validé par étape : en quittant Général, bloque tant que Nom/Archetype ne sont pas
        /// renseignés ; en quittant Guerriers, bloque tant que ValidateWarriorsStep échoue (effectif/
        /// noms des héros).</summary>
        [RelayCommand]
        private async Task Next()
        {
            if (IsGeneralTab && !ValidateGeneralStep()) return;
            if (IsWarriorsTab && !ValidateWarriorsStep()) return;
            if (SelectedTab >= StepCount - 1) return;
            SelectedTab++;

            if (IsWarriorsTab || IsEquipmentTab) await EnsureRecruitableArchetypesLoadedAsync();
        }

        /// <summary>Mode assistant uniquement (bouton Précédent).</summary>
        [RelayCommand]
        private void Back()
        {
            if (SelectedTab > 0) SelectedTab--;
        }

        /// <summary>Seul point d'écriture en base de tout le dialog - la bande d'abord (INSERT via
        /// CreateWarbandAsync si Item.Id == 0, sinon UPDATE via SaveWarbandAsync), puis chaque recrue en
        /// attente (RecruitRows aplaties en Warrior + WarriorEquipment ici, pas avant). Item.Id == 0 est
        /// le seul cas atteignable aujourd'hui (WarbandListViewModel n'ouvre ce dialog qu'en création) -
        /// la branche Update est gardée correcte si un futur appelant réutilise ce dialog pour éditer une
        /// bande existante.</summary>
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
                    created.Treasury = Archetype!.StartingTreasury - TotalSpent;
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
                        {
                            var warrior = await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, slot.Name.Trim());
                            foreach (var pick in slot.Equipment)
                                await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, materialRule: pick.MaterialRule);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < row.Count; i++)
                        {
                            var warrior = await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, row.Archetype.Name);
                            foreach (var pick in row.GroupEquipment)
                                await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, materialRule: pick.MaterialRule);
                        }
                    }
                }
            });

            Close(true);
        }
    }
}
