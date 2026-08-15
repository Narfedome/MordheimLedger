using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit
{
    /// <summary>4 étapes (Général/Guerriers/Équipement/Noms) : Règles/Magie n'ont pas de sens ici - Item
    /// est un Warband (l'instance jouée), qui n'a pas sa propre copie de ces catalogues, elle référence
    /// ceux de son WarbandArchetype (consultables via ShowArchetypeDetail/le Codex). Général = Nom de la
    /// bande + choix de l'Archetype (ChipItemView, sélection unique obligatoire) ; Guerriers = un
    /// WarriorRecruitRow par type recrutable (WarriorRecruitListView : juste un compteur 0/MaxCount, pas
    /// de nom individuel ici) ; Équipement = achat par sous-groupe nommé pour les Hommes de main
    /// (HenchmanGroupDraft, même équipement au sein d'un groupe - livre des règles, SplitHenchmanGroupDraft pour en
    /// détacher un second) et par individu pour les Héros (WarriorNameSlot.Equipment, chacun peut
    /// différer) ; Noms = un champ par recrue Héros/groupe d'Hommes de main, une fois l'équipement connu -
    /// séparé de Guerriers/Équipement pour que le joueur nomme en connaissant déjà l'équipement de chacun
    /// (PopulateSuggestedNames reprend l'étiquette déjà numérotée à l'étape Équipement, voir
    /// WarriorNameSlot.ArchetypeLabel). Tout est aplati en vrais
    /// Warrior/WarriorEquipment seulement au Save - rien n'est persisté avant.</summary>
    public partial class WarbandEditDialogViewModel : DialogViewModel<bool>
    {
        private const int StepCount = 4;

        private readonly IWarbandArchetypePickerService _warbandArchetypePicker;
        private readonly IWarbandService _warbandService;
        private readonly ILibraryService _libraryService;
        private readonly IDetailDialogService _detailDialogs;
        private readonly IEquipmentPickerService _equipmentPicker;
        private readonly ISkillPickerService _skillPicker;
        private readonly ISpellPickerService _spellPicker;
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
        [NotifyPropertyChangedFor(nameof(IsNamesTab))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyPropertyChangedFor(nameof(IsLastStep))]
        [NotifyPropertyChangedFor(nameof(StepLabel))]
        private int selectedTab;
        public bool IsGeneralTab => SelectedTab == 0;
        public bool IsWarriorsTab => SelectedTab == 1;
        public bool IsEquipmentTab => SelectedTab == 2;
        public bool IsNamesTab => SelectedTab == 3;

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

        /// <summary>Coût de recrutement de chaque ligne + équipement de chaque sous-groupe d'Hommes de
        /// main (coût × effectif de CE sous-groupe, pas de la ligne entière - deux sous-groupes du même
        /// type peuvent avoir des équipements différents) + équipement individuel (chaque slot Héros).</summary>
        private int TotalSpent => RecruitRows.Sum(r => r.Count * r.Cost)
            + RecruitRows.SelectMany(r => r.HenchmanGroupDrafts).Sum(g => g.Equipment.Sum(e => e.Cost) * g.Count)
            + RecruitRows.SelectMany(r => r.NameSlots).Sum(s => s.Equipment.Sum(e => e.Cost));

        /// <summary>En mode Bande existante, la trésorerie affichée reste fixée à ce que l'utilisateur a
        /// saisi (TreasuryOverride) - jamais décrémentée par les recrues/achats, contrairement au mode
        /// création normale.</summary>
        private int RemainingTreasury => RecruitmentRules.CalculateRemainingTreasury(Archetype?.StartingTreasury ?? 0, TotalSpent, IsExistingWarband, TreasuryOverride);

        [ObservableProperty]
        private string? warriorsError;

        [ObservableProperty]
        private string? namesError;

        /// <summary>Coché = on importe une bande déjà jouée sur papier plutôt qu'un recrutement neuf :
        /// trésorerie libre (TreasuryOverride), aucun contrôle budgétaire (recrutement/achats), et
        /// possibilité d'assigner des compétences déjà apprises pendant l'étape Équipement (voir
        /// WarriorNameSlot.Skills/HenchmanGroupDraft.Skills). Uniquement pertinent en IsWizardMode.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingTreasuryDisplay))]
        private bool isExistingWarband;

        /// <summary>Trésorerie saisie librement par l'utilisateur en mode Bande existante - remplace
        /// StartingTreasury - TotalSpent (voir RemainingTreasury) puisque les achats/recrues ne doivent
        /// pas être décomptés dans ce mode.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingTreasuryDisplay))]
        private int treasuryOverride;

        partial void OnIsExistingWarbandChanged(bool value)
        {
            if (value && Archetype is not null) TreasuryOverride = Archetype.StartingTreasury;
            UpdateRecruitability();
            if (value) ShowExistingWarbandHint();
        }

        /// <summary>Même idiome que l'avertissement de limite d'armes (ShowInfoAsync fire-and-forget
        /// depuis un callback synchrone) - OnIsExistingWarbandChanged ne peut pas être async (signature
        /// imposée par le générateur ObservableProperty).</summary>
        private async void ShowExistingWarbandHint()
        {
            await ShowInfoAsync(Loc["WarbandsExistingWarbandTitle"], Loc["WarbandsExistingWarbandHint"]);
        }

        public WarbandEditDialogViewModel(Warband item, string title, IWarbandArchetypePickerService warbandArchetypePicker,
            IWarbandService warbandService, ILibraryService libraryService, IDetailDialogService detailDialogs, IEquipmentPickerService equipmentPicker,
            ISkillPickerService skillPicker, ISpellPickerService spellPicker)
        {
            this.item = item;
            this.title = title;
            _warbandArchetypePicker = warbandArchetypePicker;
            _warbandService = warbandService;
            _libraryService = libraryService;
            _detailDialogs = detailDialogs;
            _equipmentPicker = equipmentPicker;
            _skillPicker = skillPicker;
            _spellPicker = spellPicker;
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
        private async Task ShowNamesTab()
        {
            SelectedTab = 3;
            await EnsureRecruitableArchetypesLoadedAsync();
            PopulateSuggestedNames();
        }

        [RelayCommand]
        private async Task AddArchetype()
        {
            var picked = await _warbandArchetypePicker.PickWarbandArchetypeAsync();
            if (picked is null || picked.Id == 0) return;

            Archetype = picked;
            ArchetypeError = null;
            if (IsExistingWarband) TreasuryOverride = picked.StartingTreasury;

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

            await _detailDialogs.ShowWarbandArchetypeDetailDialogAsync(fullWarband);
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
            await _detailDialogs.ShowWarriorArchetypeDetailDialogAsync(fullWarrior, Array.Empty<NamedRef>());
        }

        /// <summary>Un guerrier de plus de ce type : bloqué si son MaxCount, l'effectif max de la bande ou
        /// la trésorerie restante ne le permet plus (IncrementCommand.CanIncrement reflète déjà ça sur
        /// le bouton, ce garde-fou couvre le cas où l'état a changé entre-temps). Héros : ajoute un slot
        /// de nom vide, à renseigner avant de pouvoir Enregistrer (voir ValidateWarriorsStep). Hommes de
        /// main : grossit le dernier HenchmanGroupDraft (ou en crée un premier, nommé d'après l'archétype) -
        /// SplitHenchmanGroupDraft est le seul moyen d'en avoir plusieurs.</summary>
        [RelayCommand]
        private void IncrementWarrior(WarriorRecruitRow row)
        {
            if (Archetype is null) return;
            if (!RecruitmentRules.CanRecruit(row.Count, row.Archetype.MaxCount, RecruitRows.Sum(r => r.Count),
                    Archetype.MaxWarriors, IsExistingWarband, RemainingTreasury, row.Cost)) return;

            row.Count++;
            if (row.IsHero)
            {
                row.NameSlots.Add(new WarriorNameSlot(row));
                RenumberHeroLabels(row);
            }
            else if (row.HenchmanGroupDrafts.Count == 0) row.HenchmanGroupDrafts.Add(new HenchmanGroupDraft(row, row.Archetype.Name, 1));
            else row.HenchmanGroupDrafts[^1].Count++;
            UpdateRecruitability();
        }

        [RelayCommand]
        private void DecrementWarrior(WarriorRecruitRow row)
        {
            if (row.Count == 0) return;
            row.Count--;
            if (row.IsHero && row.NameSlots.Count > 0)
            {
                row.NameSlots.RemoveAt(row.NameSlots.Count - 1);
                RenumberHeroLabels(row);
            }
            else if (!row.IsHero && row.HenchmanGroupDrafts.Count > 0)
            {
                var last = row.HenchmanGroupDrafts[^1];
                last.Count--;
                if (last.Count <= 0) row.HenchmanGroupDrafts.RemoveAt(row.HenchmanGroupDrafts.Count - 1);
            }
            UpdateRecruitability();
        }

        /// <summary>Détache un second HenchmanGroupDraft du groupe tapé - le seul moyen d'obtenir des Hommes de
        /// main du même type équipés différemment (livre des règles : "if your Henchman group has four
        /// warriors, and you want to buy them swords, you must buy four swords" - deux équipements
        /// différents pour le même type = deux groupes). Demande combien d'unités transférer vers le
        /// nouveau groupe (1..group.Count-1, il doit toujours en rester au moins une de chaque côté) - le
        /// nouveau groupe démarre sans équipement, et les deux groupes sont renumérotés "{Archétype} 1"/
        /// "{Archétype} 2"... (RenumberHenchmanGroupDrafts) plutôt que de partager le même nom d'archétype brut,
        /// toujours personnalisable ensuite comme n'importe quel nom de groupe.</summary>
        [RelayCommand]
        private async Task SplitHenchmanGroupDraft(HenchmanGroupDraft group)
        {
            if (group.Count <= 1) return;

            var input = await ShowPromptAsync(Loc["WarbandsSplitGroupTitle"], string.Format(Loc["WarbandsSplitGroupPrompt"], group.Count - 1));
            if (!int.TryParse(input, out var moved) || moved <= 0 || moved >= group.Count) return;

            group.Count -= moved;
            var newGroup = new HenchmanGroupDraft(group.Row, group.Row.Archetype.Name, moved);
            var index = group.Row.HenchmanGroupDrafts.IndexOf(group);
            group.Row.HenchmanGroupDrafts.Insert(index + 1, newGroup);
            RenumberHenchmanGroupDrafts(group.Row);
        }

        /// <summary>Voir WarriorNameSlot.ArchetypeLabel - numérote "{Archétype} 1", "{Archétype} 2"...
        /// seulement s'il y a plusieurs héros de ce type, sinon juste le nom d'archétype brut.</summary>
        private static void RenumberHeroLabels(WarriorRecruitRow row)
        {
            var multiple = row.NameSlots.Count > 1;
            for (var i = 0; i < row.NameSlots.Count; i++)
                row.NameSlots[i].ArchetypeLabel = multiple ? $"{row.Archetype.Name} {i + 1}" : row.Archetype.Name;
        }

        /// <summary>Même idée que RenumberHeroLabels, mais sur le vrai champ Name (déjà affiché/éditable
        /// dès l'étape Équipement pour les Hommes de main, contrairement au Name des héros) - appelé après
        /// tout Split, seul moyen d'obtenir un second groupe.</summary>
        private static void RenumberHenchmanGroupDrafts(WarriorRecruitRow row)
        {
            var multiple = row.HenchmanGroupDrafts.Count > 1;
            for (var i = 0; i < row.HenchmanGroupDrafts.Count; i++)
                row.HenchmanGroupDrafts[i].Name = multiple ? $"{row.Archetype.Name} {i + 1}" : row.Archetype.Name;
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
            foreach (var row in RecruitRows)
                row.CanIncrement = RecruitmentRules.CanRecruit(row.Count, row.Archetype.MaxCount, total,
                    Archetype.MaxWarriors, IsExistingWarband, RemainingTreasury, row.Cost);
        }

        /// <summary>Achat d'équipement pour une cible : un HenchmanGroupDraft (un seul achat, appliqué à tout le
        /// sous-groupe) ou un WarriorNameSlot (Héros - propre à cette recrue). Même logique que
        /// WarriorEditDialogViewModel.AddEquipment (picker filtré par EquipmentListId/WarriorArchetypeId,
        /// choix de matériau pour les armes de corps à corps via un seul MaterialPickerDialog paginé
        /// Précédent/Suivant plutôt qu'une ActionSheet par arme, arrêt au premier objet trop cher) -
        /// simplement pas encore de WarriorId réel pour appeler AddWarriorEquipmentAsync, donc on garde
        /// des EquipmentPick en mémoire jusqu'au Save.</summary>
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
                case HenchmanGroupDraft group:
                    row = group.Row;
                    destination = group.Equipment;
                    perUnitCost = group.Count;
                    break;
                default:
                    return;
            }
            if (Archetype is null) return;

            var items = await _equipmentPicker.PickEquipmentAsync(Archetype.Id, row.Archetype.EquipmentListId, row.Archetype.Id, RemainingTreasury, perUnitCost,
                destination.Any(p => p.Item.IsFreeDagger));

            // Un seul dialog paginé pour toutes les armes de corps à corps du lot plutôt qu'une ActionSheet
            // fermée/rouverte pour chacune - voir MaterialPickerDialogViewModel. Annuler le dialog revient
            // à choisir "Normal" pour toutes (même comportement qu'annuler l'ancienne ActionSheet par arme).
            // File plutôt que Dictionary&lt;EquipmentItem, ...&gt; : items peut contenir le MÊME
            // EquipmentItem plusieurs fois (le picker permet d'acheter plusieurs exemplaires d'un même
            // objet, voir EquipmentItemViewModel.ConfirmSelection) - un dictionnaire écraserait le premier
            // choix (ex. Gromril sur la 1re épée longue) par le second (Normal sur la 2e), les deux
            // partageant la même clé. La file consomme les choix dans le même ordre que meleeItems, qui
            // suit lui-même l'ordre de items - correct même avec des objets non-armes intercalés.
            var meleeMaterials = new Queue<SpecialRule?>();
            var meleeItems = items.Where(i => i.Category == EquipmentCategory.MeleeWeapon).ToList();
            if (meleeItems.Count > 0)
            {
                var materialRules = (await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language))
                    .Where(r => r.CostMultiplier.HasValue).ToList();
                if (materialRules.Count > 0)
                {
                    // hasFreeDaggerSlot suit l'ordre des items comme la boucle d'achat plus bas, pour que
                    // le prix affiché ici (MaterialChoice.isFreeEligible) corresponde exactement à ce qui
                    // sera effectivement facturé - seule la PREMIÈRE dague du lot (existante ou dans ce
                    // même lot) est éligible, achetée en "Normal" (voir MaterialChoice, un matériau
                    // Gromril/Ithilmar reste payant même sur cette dague-là).
                    var hasFreeDaggerSlot = destination.Any(p => p.Item.IsFreeDagger);
                    var choices = new List<MaterialChoice>();
                    foreach (var item in meleeItems)
                    {
                        choices.Add(new MaterialChoice(item, materialRules, Loc["WarriorsMaterialNormal"], EquipmentPricing.IsFreeDaggerEligible(item.IsFreeDagger, hasFreeDaggerSlot)));
                        if (item.IsFreeDagger) hasFreeDaggerSlot = true;
                    }
                    var confirmed = await ShowDialogAsync(new MaterialPickerDialog(new MaterialPickerDialogViewModel(choices)));
                    foreach (var choice in choices)
                        meleeMaterials.Enqueue(confirmed == true ? choice.SelectedMaterial : null);
                }
            }

            foreach (var equipmentItem in items)
            {
                var materialRule = equipmentItem.Category == EquipmentCategory.MeleeWeapon && meleeMaterials.Count > 0
                    ? meleeMaterials.Dequeue()
                    : null;
                var pick = new EquipmentPick(equipmentItem, materialRule)
                {
                    // La première dague est gratuite par guerrier/groupe, uniquement en "Normal" (livre des
                    // règles : "in addition to his free dagger") - une deuxième dague, ou un matériau
                    // délibérément choisi sur celle-ci, coûte le prix normal et compte dans la limite
                    // d'armes (voir EquipmentItem.IsFreeDagger/WeaponLimits).
                    IsFree = EquipmentPricing.IsFreeDaggerEligible(equipmentItem.IsFreeDagger, destination.Any(p => p.Item.IsFreeDagger)) && materialRule is null
                };

                // Coût total si on achète maintenant (perUnitCost = l'effectif du groupe pour un Homme de
                // main, 1 pour un Héros) - sélection multiple : on s'arrête au premier objet trop cher
                // plutôt que de tout annuler, même logique que WarriorEditDialogViewModel.AddEquipment.
                if (!IsExistingWarband && RemainingTreasury < pick.Cost * perUnitCost)
                {
                    await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                    break;
                }

                destination.Add(pick);
            }

            // Avertissement non-bloquant (2 armes de corps à corps / 2 armes de tir différentes max par
            // guerrier, livre des règles "Starting a Warband") - certaines règles spéciales de bande
            // (ex. Combat de Queue Skaven) autorisent à dépasser, donc jamais bloquant - voir WeaponLimits.
            if (WeaponLimits.ExceedsLimits(destination.Select(p => p.Item)))
            {
                var warriorLabel = target switch
                {
                    WarriorNameSlot { Name.Length: > 0 } nameSlot => nameSlot.Name,
                    HenchmanGroupDraft group => group.Name,
                    _ => row.Name
                };
                await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], warriorLabel));
            }

            UpdateRecruitability();
        }

        /// <summary>Tap sur un chip d'équipement acheté (groupe ou slot Héros) - même recap qu'ailleurs
        /// dans l'app (EquipmentListEditDialogViewModel.ShowItemDetail, EquipmentItemViewModel.ShowDetails),
        /// pas juste le mini-popup Nom+Description générique (ChipDetailDialog) : un objet d'équipement a
        /// coût/rareté/restrictions propres, pas seulement une description.</summary>
        [RelayCommand]
        private Task ShowEquipmentDetail(EquipmentPick pick) => _detailDialogs.ShowEquipmentDetailDialogAsync(pick.Item, pick.MaterialRule);

        /// <summary>Retire un EquipmentPick de quelle que collection le contient (Equipment d'un
        /// HenchmanGroupDraft ou d'un slot Héros) - identité de référence, pas besoin de savoir d'avance
        /// laquelle puisque chaque instance n'est ajoutée qu'à une seule collection.</summary>
        [RelayCommand]
        private void RemoveEquipment(EquipmentPick pick)
        {
            foreach (var row in RecruitRows)
            {
                foreach (var group in row.HenchmanGroupDrafts)
                {
                    if (group.Equipment.Remove(pick))
                    {
                        UpdateRecruitability();
                        return;
                    }
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

        /// <summary>Mode Bande existante uniquement - assigne une ou plusieurs compétences déjà apprises à
        /// un HenchmanGroupDraft ou un WarriorNameSlot, en mémoire jusqu'au Save (voir WarriorNameSlot.Skills/
        /// HenchmanGroupDraft.Skills). Même cible que AddEquipment.</summary>
        [RelayCommand]
        private async Task AddSkill(object target)
        {
            WarriorRecruitRow row;
            ObservableCollection<Skill> destination;
            switch (target)
            {
                case WarriorNameSlot slot:
                    row = slot.Row;
                    destination = slot.Skills;
                    break;
                case HenchmanGroupDraft group:
                    row = group.Row;
                    destination = group.Skills;
                    break;
                default:
                    return;
            }
            if (Archetype is null) return;

            var skills = await _skillPicker.PickSkillAsync(Archetype.Id, row.Archetype.Id, row.Archetype.AllowedSkillCategories);
            foreach (var skill in skills)
                destination.Add(skill);
        }

        [RelayCommand]
        private Task ShowSkillDetail(Skill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill);

        /// <summary>Retire une compétence de quelle que collection la contient - même idiome que
        /// RemoveEquipment.</summary>
        [RelayCommand]
        private void RemoveSkill(Skill skill)
        {
            foreach (var row in RecruitRows)
            {
                foreach (var group in row.HenchmanGroupDrafts)
                {
                    if (group.Skills.Remove(skill)) return;
                }
                foreach (var slot in row.NameSlots)
                {
                    if (slot.Skills.Remove(skill)) return;
                }
            }
        }

        /// <summary>Mode Bande existante uniquement, sous-onglet Sorts (masqué si le type recruté n'est
        /// pas lanceur de sorts - voir RecruitSlot.IsSpellcaster) - assigne un ou plusieurs sorts déjà
        /// appris, filtrés par les écoles de magie de la bande (Archetype.MagicSchools, déjà pleinement
        /// résolu par le picker - voir GetWarbandArchetypesAsync). Même cible/idiome qu'AddSkill.</summary>
        [RelayCommand]
        private async Task AddSpell(object target)
        {
            ObservableCollection<Spell> destination;
            switch (target)
            {
                case WarriorNameSlot slot:
                    destination = slot.Spells;
                    break;
                case HenchmanGroupDraft group:
                    destination = group.Spells;
                    break;
                default:
                    return;
            }
            if (Archetype is null) return;

            var magicSchoolIds = Archetype.MagicSchools.Select(s => s.Id).ToList();
            var spells = await _spellPicker.PickSpellsAsync(magicSchoolIds);
            foreach (var spell in spells)
                destination.Add(spell);
        }

        /// <summary>Sort de départ d'un lanceur de sorts fraîchement recruté (hors mode Bande existante,
        /// où AddSpell reste un choix libre - importer une bande déjà jouée, c'est enregistrer un
        /// historique déjà déterminé, pas faire un nouveau tirage). Livre des règles : "A Wizard starts
        /// with one spell, determined randomly - roll 1D6 on the appropriate list", pas un choix libre -
        /// voir SpellRules.RollDice. Plafonné à un seul sort (bouton masqué une fois tiré, voir
        /// WarbandEditDialog.xaml) ; retirer la puce (RemoveSpellCommand, déjà câblé) permet de relancer.</summary>
        [RelayCommand]
        private async Task RollStartingSpell(object target)
        {
            ObservableCollection<Spell> destination;
            switch (target)
            {
                case WarriorNameSlot slot:
                    destination = slot.Spells;
                    break;
                case HenchmanGroupDraft group:
                    destination = group.Spells;
                    break;
                default:
                    return;
            }
            if (Archetype is null) return;

            var magicSchoolIds = Archetype.MagicSchools.Select(s => s.Id).ToHashSet();
            var available = (await _libraryService.GetSpellsAsync(LocalizationService.Instance.Language))
                .Where(s => magicSchoolIds.Contains(s.MagicSchoolId)).ToList();
            if (available.Count == 0) return;

            var roll = SpellRules.RollDice();
            var spell = available.FirstOrDefault(s => s.RollValue == roll);
            if (spell is null)
            {
                await ShowInfoAsync(Loc["WarbandsSpellRollTitle"], Loc["WarbandsSpellRollEmptyMessage"]);
                return;
            }

            destination.Add(spell);
            await ShowInfoAsync(Loc["WarbandsSpellRollTitle"], string.Format(Loc["WarbandsSpellRollResult"], roll, spell.Name));
        }

        [RelayCommand]
        private Task ShowSpellDetail(Spell spell) => _detailDialogs.ShowSpellDetailDialogAsync(spell);

        /// <summary>Retire un sort de quelle que collection le contient - même idiome que RemoveSkill.</summary>
        [RelayCommand]
        private void RemoveSpell(Spell spell)
        {
            foreach (var row in RecruitRows)
            {
                foreach (var group in row.HenchmanGroupDrafts)
                {
                    if (group.Spells.Remove(spell)) return;
                }
                foreach (var slot in row.NameSlots)
                {
                    if (slot.Spells.Remove(spell)) return;
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
        /// MinCount). Les noms se valident séparément, à l'étape Noms (voir ValidateNamesStep).</summary>
        private bool ValidateWarriorsStep()
        {
            if (Archetype is null) return false;

            var total = RecruitRows.Sum(r => r.Count);
            if (!RecruitmentRules.MeetsMinWarriors(total, Archetype.MinWarriors))
            {
                WarriorsError = string.Format(Loc["WarbandsMinWarriorsError"], Archetype.MinWarriors);
                return false;
            }

            foreach (var row in RecruitRows.Where(r => !RecruitmentRules.MeetsMinCount(r.Count, r.Archetype.MinCount)))
            {
                WarriorsError = string.Format(Loc["WarbandsRequiredWarriorMissing"], row.Name, row.Archetype.MinCount);
                return false;
            }

            WarriorsError = null;
            return true;
        }

        /// <summary>Étape Noms : un nom renseigné pour chaque recrue Héros et chaque sous-groupe
        /// d'Hommes de main (livre des règles : "you will need to... name each Henchman group") -
        /// PopulateSuggestedNames pré-remplit déjà tout à l'entrée de cette étape, donc ce garde-fou ne
        /// mord que si le joueur a vidé un champ après coup.</summary>
        private bool ValidateNamesStep()
        {
            foreach (var row in RecruitRows.Where(r => r.IsHero))
            {
                if (row.NameSlots.Any(slot => string.IsNullOrWhiteSpace(slot.Name)))
                {
                    NamesError = string.Format(Loc["WarbandsWarriorNameRequired"], row.Name);
                    return false;
                }
            }

            foreach (var row in RecruitRows.Where(r => !r.IsHero))
            {
                if (row.HenchmanGroupDrafts.Any(group => string.IsNullOrWhiteSpace(group.Name)))
                {
                    NamesError = string.Format(Loc["WarbandsWarriorNameRequired"], row.Name);
                    return false;
                }
            }

            NamesError = null;
            return true;
        }

        /// <summary>Pré-remplit le nom de chaque héros à l'entrée de l'étape Noms, avec l'étiquette déjà
        /// affichée à l'étape Équipement (WarriorNameSlot.ArchetypeLabel, tenue à jour par
        /// RenumberHeroLabels) - ne touche jamais un nom déjà personnalisé par le joueur (non vide). Les
        /// groupes d'Hommes de main n'ont rien à faire ici : leur Name est déjà tenu à jour en direct par
        /// RenumberHenchmanGroupDrafts, dès l'étape Équipement.</summary>
        private void PopulateSuggestedNames()
        {
            foreach (var row in RecruitRows.Where(r => r.IsHero && r.Count > 0))
            {
                foreach (var slot in row.NameSlots)
                {
                    if (string.IsNullOrWhiteSpace(slot.Name))
                        slot.Name = slot.ArchetypeLabel;
                }
            }
        }

        /// <summary>Mode assistant uniquement (bouton Suivant) - avance d'une étape, Général→Guerriers→
        /// Équipement→Noms. Validé par étape : en quittant Général, bloque tant que Nom/Archetype ne sont
        /// pas renseignés ; en quittant Guerriers, bloque tant que ValidateWarriorsStep échoue (effectif
        /// minimum). Les noms eux-mêmes ne sont validés qu'à l'étape Noms (ValidateNamesStep, via Save).</summary>
        [RelayCommand]
        private async Task Next()
        {
            if (IsGeneralTab && !ValidateGeneralStep()) return;
            if (IsWarriorsTab && !ValidateWarriorsStep()) return;
            if (SelectedTab >= StepCount - 1) return;
            SelectedTab++;

            if (IsWarriorsTab || IsEquipmentTab) await EnsureRecruitableArchetypesLoadedAsync();
            if (IsNamesTab) PopulateSuggestedNames();
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

            PopulateSuggestedNames();
            if (!ValidateNamesStep())
            {
                SelectedTab = 3;
                return;
            }

            await Loading.RunAsync(async () =>
            {
                int warbandId;
                if (Item.Id == 0)
                {
                    var created = await _warbandService.CreateWarbandAsync(Item.Name, Archetype!);
                    warbandId = created.Id;
                    created.Treasury = IsExistingWarband ? TreasuryOverride : Archetype!.StartingTreasury - TotalSpent;
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
                            foreach (var skill in slot.Skills)
                                await _warbandService.AddWarriorSkillAsync(warrior.Id, skill);
                            foreach (var spell in slot.Spells)
                                await _warbandService.AddWarriorSpellAsync(warrior.Id, spell);
                            if (IsExistingWarband && slot.Experience != warrior.Experience)
                            {
                                warrior.Experience = slot.Experience;
                                await _warbandService.SaveWarriorAsync(warrior);
                            }
                        }
                    }
                    else
                    {
                        // Un seul Warrior par groupe (HeadCount = l'effectif) plutôt qu'une ligne par
                        // individu - le groupe est mécaniquement une seule entité (livre des règles :
                        // XP/équipement/compétences partagés par tout le groupe), voir Warrior.HeadCount.
                        foreach (var group in row.HenchmanGroupDrafts)
                        {
                            var warrior = await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, group.Name.Trim(), headCount: group.Count);
                            foreach (var pick in group.Equipment)
                                await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, materialRule: pick.MaterialRule);
                            foreach (var skill in group.Skills)
                                await _warbandService.AddWarriorSkillAsync(warrior.Id, skill);
                            foreach (var spell in group.Spells)
                                await _warbandService.AddWarriorSpellAsync(warrior.Id, spell);
                            if (IsExistingWarband && group.Experience != warrior.Experience)
                            {
                                warrior.Experience = group.Experience;
                                await _warbandService.SaveWarriorAsync(warrior);
                            }
                        }
                    }
                }
            });

            Close(true);
        }
    }
}
