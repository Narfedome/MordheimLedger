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
using System.Windows.Input;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit
{
    /// <summary>6 étapes (Général/Guerriers/Équipement/Noms/Mercenaires/Noms) : Règles/Magie n'ont pas de
    /// sens ici - Item est un Warband (l'instance jouée), qui n'a pas sa propre copie de ces catalogues,
    /// elle référence ceux de son WarbandArchetype (consultables via ShowArchetypeDetail/le Codex).
    /// Général = Nom de la bande + choix de l'Archetype (ChipItemView, sélection unique obligatoire) ;
    /// Guerriers = un WarriorRecruitRow par type recrutable (WarriorRecruitListView : juste un compteur
    /// 0/MaxCount, pas de nom individuel ici) ; Équipement = achat par sous-groupe nommé pour les Hommes
    /// de main (HenchmanGroupDraft, même équipement au sein d'un groupe - livre des règles,
    /// SplitHenchmanGroupDraft pour en détacher un second) et par individu pour les Héros
    /// (WarriorNameSlot.Equipment, chacun peut différer) ; Noms (Héros/Hommes de main) = un champ par
    /// recrue Héros/groupe d'Hommes de main, une fois l'équipement connu - séparé de Guerriers/Équipement
    /// pour que le joueur nomme en connaissant déjà l'équipement de chacun (PopulateSuggestedNames reprend
    /// l'étiquette déjà numérotée à l'étape Équipement, voir WarriorNameSlot.ArchetypeLabel) ; Mercenaires
    /// = recrutement des Francs-Tireurs (gear fixe, aucun équipement à choisir) ; Noms (Francs-Tireurs) =
    /// leur propre étape de nommage, à la toute fin - séparée de la première étape Noms le 2026-09-01
    /// (retour utilisateur : "guerrier equipement noms, franc tireur nom") plutôt qu'une 3e section
    /// partagée avec les Héros/Hommes de main. Tout est aplati en vrais Warrior/WarriorEquipment seulement
    /// au Save - rien n'est persisté avant.</summary>
    public partial class WarbandEditDialogViewModel : DialogViewModel<bool>
    {
        /// <summary>Général/Guerriers/Équipement/Noms/Mercenaires/Noms - la 6e étape (noms des
        /// Francs-Tireurs) n'existe que si au moins un Franc-Tireur est engagé (2026-09-24, retour
        /// utilisateur - rien à nommer sinon). Un Franc-Tireur reste recrutable dès la toute première
        /// création (retour utilisateur antérieur, pas seulement en Mode Libre/bande déjà existante). En
        /// création pas à pas (IsWizardMode), une étape Récapitulatif de plus, toujours la dernière (même
        /// jour, retour utilisateur - relire la bande avant de l'enregistrer).</summary>
        private int StepCount => (HasRecruitedHiredSwords ? 6 : 5) + (IsWizardMode ? 1 : 0);

        private readonly IWarbandArchetypePickerService _warbandArchetypePicker;
        private readonly IWarbandService _warbandService;
        private readonly ILibraryService _libraryService;
        private readonly IDetailDialogService _detailDialogs;
        private readonly IEquipmentPickerService _equipmentPicker;
        private readonly ISkillPickerService _skillPicker;
        private readonly ISpellPickerService _spellPicker;
        private readonly IInjuryPickerService _injuryPicker;
        private readonly IMutationPickerService _mutationPicker;
        private readonly IHiredSwordPickerService _hiredSwordPicker;
        private bool _recruitableLoaded;

        /// <summary>Guerriers déjà en base retirés cette session (decrement confirmé sous leur effectif
        /// d'origine, voir DecrementWarrior) - le slot correspondant quitte NameSlots/HenchmanGroupDrafts
        /// immédiatement, donc Save() ne le reverrait plus dans sa boucle normale ; cette liste est le
        /// seul endroit qui se souvient encore de la suppression à effectuer (avec remboursement) au
        /// moment d'Enregistrer. Vidée jamais explicitement : ne vit que le temps d'un Save() réussi
        /// (Close(true) juste après), et le ViewModel n'est pas réutilisé après.</summary>
        private readonly List<Warrior> _pendingFullDeletions = new();

        public bool IsWizardMode { get; }
        protected override bool CancelResult => false;

        /// <summary>Voir DialogViewModel.EditableState - les lignes de recrutement sont projetées sur leurs
        /// seules valeurs saisies (effectif, noms, XP, équipement/compétences/sorts par slot), pas
        /// sérialisées telles quelles (commandes, archétypes complets, état d'affichage...).</summary>
        protected override object? EditableState => new object?[]
        {
            Item, Archetype?.Id, IsExistingWarband, TreasuryOverride,
            RecruitRows.Select(r => new object?[]
            {
                r.Archetype.Id, r.Count,
                r.NameSlots.Select(s => new object?[] { s.Name, SlotState(s) }),
                r.HenchmanGroupDrafts.Select(g => new object?[] { g.Name, g.Count, SlotState(g) })
            }),
            HiredSwordRows.Select(h => new object?[] { h.HiredSword.Id, h.IsRecruited, h.Name })
        };

        private static object?[] SlotState(RecruitSlot slot) =>
        [
            slot.Experience,
            slot.Equipment.Select(e => new object?[] { e.Item.Id, e.MaterialRule?.Id, e.BlessingRule?.Id, e.ExistingId }),
            slot.Skills.Select(s => s.Id),
            slot.Spells.Select(s => s.Id),
            slot.Mutations.Select(m => m.Id),
            slot.Injuries.Select(i => i.Id)
        ];

        /// <summary>Masque les boutons Ajouter/Retirer de la puce Archetype hors création (Item.Id != 0,
        /// donc IsWizardMode false) - changer l'archétype d'une bande déjà recrutée invaliderait tous les
        /// WarriorArchetypeId déjà liés à ses guerriers, ça n'a plus de sens une fois la bande créée. La
        /// puce elle-même (nom/récap) reste affichée et tapable (ShowArchetypeDetailCommand, inchangé).</summary>
        public ICommand? ArchetypeAddCommandOrNull => IsWizardMode ? AddArchetypeCommand : null;
        public ICommand? ArchetypeRemoveCommandOrNull => IsWizardMode ? RemoveArchetypeCommand : null;

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
        [NotifyPropertyChangedFor(nameof(IsWarriorNamesTab))]
        [NotifyPropertyChangedFor(nameof(IsMercenariesTab))]
        [NotifyPropertyChangedFor(nameof(IsHiredSwordNamesTab))]
        [NotifyPropertyChangedFor(nameof(IsRecapTab))]
        [NotifyPropertyChangedFor(nameof(RecapLines))]
        [NotifyPropertyChangedFor(nameof(RecapTitle))]
        [NotifyPropertyChangedFor(nameof(RecapRatingDisplay))]
        [NotifyPropertyChangedFor(nameof(RecapWyrdstoneDisplay))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyPropertyChangedFor(nameof(IsLastStep))]
        [NotifyPropertyChangedFor(nameof(StepLabel))]
        private int selectedTab;
        public bool IsGeneralTab => SelectedTab == 0;
        public bool IsWarriorsTab => SelectedTab == 1;
        public bool IsEquipmentTab => SelectedTab == 2;

        /// <summary>Juste après Équipement - nommage des Héros/groupes d'Hommes de main une fois leur
        /// équipement connu (voir PopulateSuggestedNames). Distincte de la 2e étape Noms plus bas
        /// (IsHiredSwordNamesTab, Francs-Tireurs) depuis le 2026-09-01 (retour utilisateur : "guerrier
        /// equipement noms, franc tireur nom") - auparavant une 3e section partagée de la même étape,
        /// après Mercenaires.</summary>
        private const int WarriorNamesTabIndex = 3;
        public bool IsWarriorNamesTab => SelectedTab == WarriorNamesTabIndex;

        /// <summary>Juste après Noms (Héros/Hommes de main) - engagement des Francs-Tireurs, séparé de
        /// Guerriers puisqu'un Franc-Tireur n'est ni un Héros ni un Homme de main du catalogue
        /// WarriorArchetype (aucun équipement/compétence à choisir, gear fixe - voir HiredSwordRecruitRow)
        /// et ne compte jamais dans MaxWarriors/MaxCount (RecruitmentRules.CanRecruitHiredSword).
        /// Disponible dès la toute première création de bande (retour utilisateur explicite - contrairement
        /// à une décision antérieure, pas réservé au Mode Libre/bande déjà existante).</summary>
        public bool IsMercenariesTab => SelectedTab == 4;

        /// <summary>Dernière étape - nommage des Francs-Tireurs recrutés à l'étape Mercenaires juste
        /// avant, dans sa propre étape depuis le 2026-09-01 (voir WarriorNamesTabIndex's own doc) plutôt
        /// que la 3e section de la même étape que les Héros/Hommes de main.</summary>
        private const int HiredSwordNamesTabIndex = 5;
        public bool IsHiredSwordNamesTab => SelectedTab == HiredSwordNamesTabIndex && HasRecruitedHiredSwords;

        /// <summary>Dernière étape en création pas à pas (voir StepCount) - 6e ou 7e selon qu'un Franc-Tireur
        /// est engagé : lecture seule de toute la bande avant Enregistrer (RecapLines).</summary>
        public bool IsRecapTab => IsWizardMode && SelectedTab == StepCount - 1;

        public string RecapTitle => Archetype is null ? Item.Name : $"{Item.Name} — {Archetype.Name}";

        /// <summary>Mode Libre uniquement - Warband n'est pas observable, d'où une propriété du ViewModel
        /// rafraîchie à chaque changement d'étape plutôt qu'un binding direct sur Item.WyrdstoneShards.</summary>
        public string RecapWyrdstoneDisplay => $"{Loc["WarbandsWyrdstoneLabel"]} {Item.WyrdstoneShards}";

        /// <summary>Valeur de bande des recrues de cette session (Core.Rules.WarbandRatingRules - même formule
        /// que la fiche de bande), Francs-Tireurs compris.</summary>
        public string RecapRatingDisplay
        {
            get
            {
                var rating = RecruitRows.Where(r => r.Count > 0).Sum(r =>
                        r.NameSlots.Sum(s => WarbandRatingRules.WarriorContribution(r.Archetype.IsLargeCreature, s.Experience, 1, null))
                        + r.HenchmanGroupDrafts.Sum(g => WarbandRatingRules.WarriorContribution(r.Archetype.IsLargeCreature, g.Experience, g.Count, null)))
                    + RecruitedHiredSwordRows.Sum(h => WarbandRatingRules.WarriorContribution(false, 0, 1, h.HiredSword.BaseRating));
                return $"{Loc["WarbandRatingPh"]} : {rating}";
            }
        }

        /// <summary>Une ligne par Héros, groupe d'Hommes de main et Franc-Tireur recruté (voir WarbandRecapLine).</summary>
        public IReadOnlyList<WarbandRecapLine> RecapLines
        {
            get
            {
                var lines = new List<WarbandRecapLine>();
                foreach (var row in RecruitRows.Where(r => r.Count > 0))
                {
                    foreach (var slot in row.NameSlots)
                    {
                        var name = string.IsNullOrWhiteSpace(slot.Name) ? slot.DisplayLabel : slot.Name.Trim();
                        lines.Add(new WarbandRecapLine($"{name} — {row.Archetype.Name}", RecapSlotDetails(slot)));
                    }
                    foreach (var group in row.HenchmanGroupDrafts)
                        lines.Add(new WarbandRecapLine($"{group.Name.Trim()} ({group.Count}×) — {row.Archetype.Name}", RecapSlotDetails(group)));
                }
                foreach (var hiredSword in RecruitedHiredSwordRows)
                    lines.Add(new WarbandRecapLine($"{hiredSword.Name.Trim()} — {hiredSword.HiredSword.Name}", string.Empty));
                return lines;
            }
        }

        private string RecapSlotDetails(RecruitSlot slot)
        {
            var parts = new List<string>();
            void AddPart(string headingKey, IEnumerable<string> names)
            {
                var list = names.ToList();
                if (list.Count > 0) parts.Add($"{Loc[headingKey]} : {string.Join(", ", list)}");
            }

            AddPart("WarbandCreateTabEquipment", slot.Equipment.Select(e => e.Name));
            AddPart("WarriorsSkillsHeading", slot.Skills.Select(s => s.Name));
            AddPart("WarriorsSpellsHeading", slot.Spells.Select(s => s.Name));
            AddPart("WarriorsMutationsHeading", slot.Mutations.Select(m => m.Name));
            AddPart("WarriorsInjuriesHeading", slot.Injuries.Select(i => i.Name));
            if (IsExistingWarband && slot.Experience > 0) parts.Add($"{Loc["WarriorExperiencePh"]} : {slot.Experience}");
            return string.Join(" · ", parts);
        }

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

        /// <summary>Une ligne par HiredSword éligible à cet Archetype (RestrictedToWarbandArchetypeIds vide
        /// ou le contenant) - onglet Mercenaires. Peuplée par EnsureRecruitableArchetypesLoadedAsync, en
        /// parallèle de RecruitRows. Contrairement à RecruitRows, jamais réinitialisée par
        /// AddArchetype/RemoveArchetype (Franc-Tireur n'a pas de sens tant qu'aucun Archetype n'est choisi -
        /// _recruitableLoaded couvre déjà ce cas, voir EnsureRecruitableArchetypesLoadedAsync).</summary>
        [ObservableProperty]
        private ObservableCollection<HiredSwordRecruitRow> hiredSwordRows = new();

        /// <summary>Effectif total vérifié contre Archetype.MaxWarriors - Count contient déjà le roster
        /// existant (pré-rempli par EnsureRecruitableArchetypesLoadedAsync quand Item.Id != 0), pas
        /// besoin d'additionner un terme séparé.</summary>
        private int TotalWarriorCount => RecruitRows.Sum(r => r.Count);

        public string RosterCountDisplay
        {
            get
            {
                if (Archetype is null) return string.Empty;
                var total = TotalWarriorCount;
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

        /// <summary>Coût de recrutement de chaque ligne (seulement les têtes ajoutées cette session -
        /// Count - ExistingCount, le roster déjà en base est déjà payé) + équipement de chaque
        /// sous-groupe d'Hommes de main (coût × effectif de CE sous-groupe, pas de la ligne entière - deux
        /// sous-groupes du même type peuvent avoir des équipements différents) + équipement individuel
        /// (chaque slot Héros) - dans les deux cas, seuls les EquipmentPick sans ExistingId comptent (un
        /// pick avec ExistingId représente un WarriorEquipment déjà payé, voir RecruitSlot constructeur).
        /// Limite acceptée : si un groupe déjà existant grossit (nouvelle tête ajoutée à un groupe déjà
        /// équipé), un nouvel achat d'équipement pour "tout le groupe" est facturé au Count actuel
        /// (l'effectif après ajout), pas seulement pour la tête ajoutée - pas de rescaling plus fin tenté.</summary>
        /// <summary>Inclut la prime d'engagement (HireCost) de chaque nouveau Franc-Tireur coché cette
        /// session (ExistingWarrior null - un déjà recruté est déjà payé) - son équipement de départ est
        /// fixe et déjà compris dans HireCost, pas de terme séparé comme pour un Héros/groupe.</summary>
        private int TotalSpent => RecruitRows.Sum(r => (r.Count - r.ExistingCount) * r.Cost)
            + RecruitRows.SelectMany(r => r.HenchmanGroupDrafts).Sum(g => g.Equipment.Where(e => e.ExistingId is null).Sum(e => e.Cost) * g.Count)
            + RecruitRows.SelectMany(r => r.NameSlots).Sum(s => s.Equipment.Where(e => e.ExistingId is null).Sum(e => e.Cost))
            // Mutations achetées cette session (livre : "+ the cost of mutations" au recrutement) - celles
            // déjà en base (BaselineMutations) sont déjà payées.
            + RecruitRows.SelectMany(r => r.HenchmanGroupDrafts).Sum(g => NewMutationsCost(g) * g.Count)
            + RecruitRows.SelectMany(r => r.NameSlots).Sum(NewMutationsCost)
            + HiredSwordRows.Where(r => r.IsRecruited && r.ExistingWarrior is null).Sum(r => r.Cost);

        private static int NewMutationsCost(RecruitSlot slot) =>
            slot.Mutations.Where(m => slot.BaselineMutations.All(b => b.Item != m)).Sum(m => m.Cost);

        /// <summary>Ce qui doit revenir à la trésorerie suite à des actions sur le roster déjà existant -
        /// suppression complète confirmée (Cost du guerrier + son équipement d'origine, voir
        /// DecrementWarrior/_pendingFullDeletions), réduction partielle d'un groupe d'Hommes de main (Cost
        /// du type × têtes retirées) et retrait d'un objet d'équipement déjà payé (bouton "x" sur une puce
        /// d'un guerrier existant - RemoveEquipment, retiré de la collection mais jusqu'ici jamais
        /// remboursé). Les deux premiers termes ne regardent que les guerriers/groupes concernés ; le
        /// troisième parcourt tout le roster restant (les slots pleinement supprimés n'y figurent plus,
        /// déjà couverts par le premier terme via l'équipement d'origine complet du Warrior).</summary>
        private int TotalRefunds => _pendingFullDeletions.Sum(w => w.Cost + RefundableEquipmentCost(w.Equipment, _ => true) + w.Mutations.Sum(m => m.Item.Cost))
            // Mutation déjà payée retirée d'un guerrier existant - même traitement que l'équipement.
            + RecruitRows.SelectMany(r => r.NameSlots.Cast<RecruitSlot>().Concat(r.HenchmanGroupDrafts))
                .Where(s => s.ExistingWarrior != null)
                .Sum(s => s.BaselineMutations.Where(b => !s.Mutations.Contains(b.Item)).Sum(b => b.Item.Cost))
            + RecruitRows.SelectMany(r => r.HenchmanGroupDrafts.Select(g => (Row: r, Group: g)))
                .Where(t => t.Group.ExistingWarrior != null && t.Group.Count < t.Group.BaselineHeadCount)
                .Sum(t => t.Row.Cost * (t.Group.BaselineHeadCount - t.Group.Count))
            + RecruitRows.SelectMany(r => r.NameSlots.Cast<RecruitSlot>().Concat(r.HenchmanGroupDrafts))
                .Where(s => s.ExistingWarrior != null)
                .Sum(s => RefundableEquipmentCost(s.BaselineEquipment, b => s.Equipment.All(p => p.ExistingId != b.Id)))
            // Franc-Tireur déjà engagé décoché cette session - remboursement intégral de sa prime, pas de
            // confirmation (voir HiredSwordRecruitRow, point d'entrée secondaire, plus simple que
            // DecrementWarrior/_pendingFullDeletions à dessein).
            + HiredSwordRows.Where(r => !r.IsRecruited && r.ExistingWarrior != null).Sum(r => r.Cost);

        /// <summary>Combien de la baseline fournie (l'équipement d'ORIGINE d'un guerrier, avant toute
        /// modification cette session) rembourser réellement - la dague gratuite (livre des règles : "in
        /// addition to his free dagger") ne doit jamais générer de remboursement, alors que
        /// WarriorEquipment ne retient aucune trace d'avoir été gratuite à l'achat (EquipmentPick.IsFree,
        /// qui le sait, n'est jamais persisté). Reconstruit donc l'éligibilité "première dague, sans
        /// matériau" en rejouant la baseline complète dans l'ordre d'achat (Id croissant, seul ordre
        /// disponible) avec la même règle qu'à l'achat (EquipmentPricing.IsFreeDaggerEligible), puis ne
        /// somme que les entrées retenues par shouldRefund - fullBaseline doit toujours être la liste
        /// COMPLÈTE d'origine (pas déjà filtrée), sans quoi la reconstruction de l'ordre/éligibilité serait
        /// faussée.</summary>
        private static int RefundableEquipmentCost(IEnumerable<WarriorEquipment> fullBaseline, Func<WarriorEquipment, bool> shouldRefund)
        {
            var total = 0;
            var hasFreeDagger = false;
            foreach (var we in fullBaseline.OrderBy(e => e.Id))
            {
                var isFree = we.MaterialRule is null && EquipmentPricing.IsFreeDaggerEligible(we.Item.IsFreeDagger, hasFreeDagger);
                if (isFree) hasFreeDagger = true;
                if (shouldRefund(we))
                    total += EquipmentPricing.CalculateCost(we.Item.Cost, we.MaterialRule?.CostMultiplier, isFree) * we.Quantity;
            }
            return total;
        }

        /// <summary>Trésorerie de départ pour le calcul du "restant" ci-dessous : la trésorerie RÉELLE
        /// actuelle de la bande (Item.Treasury) quand on édite une bande déjà sauvegardée (Item.Id != 0),
        /// pas le trésor de départ théorique de l'archétype - une bande déjà jouée a presque toujours une
        /// trésorerie différente de son StartingTreasury d'origine (dépenses/revenus passés).</summary>
        public int RosterStartingTreasury => Item.Id != 0 ? Item.Treasury : Archetype?.StartingTreasury ?? 0;

        /// <summary>En mode Libre, la trésorerie affichée reste fixée à ce que l'utilisateur a saisi
        /// (TreasuryOverride) - jamais décrémentée par les recrues/achats, contrairement au mode Coûts
        /// appliqués. TotalRefunds vient en déduction de TotalSpent (donc s'ajoute au restant affiché) -
        /// même calcul que Save() applique réellement à Item.Treasury, pour que le montant affiché en
        /// direct pendant la session corresponde exactement à ce qui sera écrit en base.</summary>
        private int RemainingTreasury => RecruitmentRules.CalculateRemainingTreasury(RosterStartingTreasury, TotalSpent - TotalRefunds, IsExistingWarband, TreasuryOverride);

        [ObservableProperty]
        private string? warriorsError;

        /// <summary>Étape Équipement - mutation obligatoire manquante (voir ValidateEquipmentStep).</summary>
        [ObservableProperty]
        private string? equipmentError;

        [ObservableProperty]
        private string? namesError;

        /// <summary>Même principe que NamesError mais pour la 2e étape Noms (Francs-Tireurs, voir
        /// IsHiredSwordNamesTab) - une erreur propre, pas partagée avec NamesError, pour que chaque étape
        /// garde son propre message plutôt qu'un état résiduel de l'autre.</summary>
        [ObservableProperty]
        private string? hiredSwordNamesError;

        /// <summary>Coché = on importe une bande déjà jouée sur papier plutôt qu'un recrutement neuf :
        /// trésorerie libre (TreasuryOverride), aucun contrôle budgétaire (recrutement/achats), et
        /// possibilité d'assigner des compétences/sorts déjà appris pendant l'étape Équipement (voir
        /// WarriorNameSlot.Skills/HenchmanGroupDraft.Skills). Pertinent dans les deux modes (création ET
        /// édition d'une bande déjà sauvegardée, voir Item.Id) - libellé XAML "Mode libre" plutôt que
        /// "Bande existante" (qui ne fait plus sens hors création), même flag et même comportement dans
        /// les deux cas. Transmis à EditExistingWarrior comme skipCosts pour que "Roster actuel" respecte
        /// aussi ce mode.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingTreasuryDisplay))]
        private bool isExistingWarband;

        /// <summary>Trésorerie saisie librement par l'utilisateur en mode Libre - remplace
        /// RosterStartingTreasury - TotalSpent (voir RemainingTreasury) puisque les achats/recrues ne
        /// doivent pas être décomptés dans ce mode.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingTreasuryDisplay))]
        private int treasuryOverride;

        partial void OnIsExistingWarbandChanged(bool value)
        {
            if (value) TreasuryOverride = RosterStartingTreasury;

            // Propage rétroactivement aux slots déjà créés (le joueur a coché/décoché après être
            // retourné en arrière depuis l'étape Équipement) - sans ça, RecruitSlot.IsExistingWarband
            // resterait figé sur sa valeur de construction. Réinitialise aussi SelectedSection au défaut
            // du nouveau mode (voir RecruitSlot constructeur) : sans ça, un slot resterait bloqué sur un
            // onglet qui vient de devenir masqué (ex. "Compétences" en quittant Bande existante).
            foreach (var row in RecruitRows)
            {
                foreach (var slot in row.NameSlots)
                {
                    slot.IsExistingWarband = value;
                    slot.SelectedSection = slot.DefaultSection;
                }
                foreach (var group in row.HenchmanGroupDrafts)
                {
                    group.IsExistingWarband = value;
                    group.SelectedSection = group.DefaultSection;
                }
            }

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
            ISkillPickerService skillPicker, ISpellPickerService spellPicker, IInjuryPickerService injuryPicker,
            IMutationPickerService mutationPicker, IHiredSwordPickerService hiredSwordPicker)
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
            _injuryPicker = injuryPicker;
            _mutationPicker = mutationPicker;
            _hiredSwordPicker = hiredSwordPicker;
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
        private async Task ShowWarriorNamesTab()
        {
            SelectedTab = WarriorNamesTabIndex;
            await EnsureRecruitableArchetypesLoadedAsync();
            PopulateSuggestedNames();
        }

        [RelayCommand]
        private async Task ShowMercenariesTab()
        {
            SelectedTab = 4;
            await EnsureRecruitableArchetypesLoadedAsync();
        }

        [RelayCommand]
        private async Task ShowHiredSwordNamesTab()
        {
            SelectedTab = HiredSwordNamesTabIndex;
            await EnsureRecruitableArchetypesLoadedAsync();
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
            // de l'ancien archetype, plus valides pour le nouveau (idem éligibilité Franc-Tireur).
            _recruitableLoaded = false;
            RecruitRows.Clear();
            HiredSwordRows.Clear();
        }

        [RelayCommand]
        private void RemoveArchetype()
        {
            Archetype = null;
            _recruitableLoaded = false;
            RecruitRows.Clear();
            HiredSwordRows.Clear();
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

        /// <summary>Charge le catalogue recrutable ET, si Item.Id != 0 (bande rouverte pour édition), le
        /// roster déjà en base - fusionnés en un seul passage plutôt que deux méthodes séparées : le
        /// roster existant se pré-remplit directement DANS RecruitRows (un WarriorNameSlot/
        /// HenchmanGroupDraft par Warrior déjà recruté, voir RecruitSlot.ExistingWarrior), pas dans une
        /// liste à part - retour utilisateur explicite : l'édition doit rester le même wizard 4 étapes que
        /// la création, pas un flux parallèle.</summary>
        private async Task EnsureRecruitableArchetypesLoadedAsync()
        {
            if (_recruitableLoaded || Archetype is null) return;
            // Le roster existant pré-remplit RecruitRows (bande rouverte) - pas une modification de
            // l'utilisateur, voir EditableState.
            await RunBaselineNeutralAsync(LoadRecruitableArchetypesAsync);
        }

        private async Task LoadRecruitableArchetypesAsync()
        {
            if (Archetype is null) return;
            var language = LocalizationService.Instance.Language;
            List<WarriorArchetype> loaded = new();
            List<HiredSword> loadedHiredSwords = new();
            List<Warrior> existingWarriors = new();
            await Loading.RunAsync(async () =>
            {
                loaded = await Task.Run(() => _libraryService.GetWarriorArchetypesAsync(Archetype.Id, language));
                loadedHiredSwords = await Task.Run(() => _libraryService.GetHiredSwordsAsync(language));
                if (Item.Id != 0)
                    existingWarriors = await Task.Run(() => _warbandService.GetWarriorsAsync(Item.Id, language));
            });

            var isEditingWarband = Item.Id != 0;
            var rows = new ObservableCollection<WarriorRecruitRow>();
            foreach (var archetype in loaded)
            {
                var row = new WarriorRecruitRow(archetype, isEditingWarband);
                // Triés par Id pour un ordre stable (ordre de recrutement d'origine) - plusieurs groupes
                // d'Hommes de main du même archétype si le joueur en avait déjà scindé un (voir
                // SplitHenchmanGroupDraft), chacun devient son propre HenchmanGroupDraft ici.
                foreach (var w in existingWarriors.Where(w => w.WarriorArchetypeId == archetype.Id && w.Status != WarriorStatus.Dead && w.Status != WarriorStatus.Retired).OrderBy(w => w.Id))
                {
                    if (w.IsHero)
                        row.NameSlots.Add(new WarriorNameSlot(row, IsExistingWarband, existingWarrior: w));
                    else
                        row.HenchmanGroupDrafts.Add(new HenchmanGroupDraft(row, w.Name, w.HeadCount, IsExistingWarband, existingWarrior: w));
                    row.Count += w.HeadCount;
                }
                rows.Add(row);
            }
            RecruitRows = rows;

            var hiredSwordRowsList = new ObservableCollection<HiredSwordRecruitRow>();
            foreach (var hiredSword in loadedHiredSwords.Where(h => h.RestrictedToWarbandArchetypeIds.Count == 0 || h.RestrictedToWarbandArchetypeIds.Contains(Archetype.Id)))
            {
                var existing = existingWarriors.FirstOrDefault(w => w.HiredSwordId == hiredSword.Id && w.Status != WarriorStatus.Dead && w.Status != WarriorStatus.Retired);
                hiredSwordRowsList.Add(new HiredSwordRecruitRow(hiredSword, existing));
            }
            HiredSwordRows = hiredSwordRowsList;

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
            if (!RecruitmentRules.CanRecruit(row.Count, row.Archetype.MaxCount, TotalWarriorCount,
                    Archetype.MaxWarriors, IsExistingWarband, RemainingTreasury, row.Cost)) return;

            row.Count++;
            if (row.IsHero)
            {
                row.NameSlots.Add(new WarriorNameSlot(row, IsExistingWarband));
                RenumberHeroLabels(row);
            }
            else if (row.HenchmanGroupDrafts.Count == 0) row.HenchmanGroupDrafts.Add(new HenchmanGroupDraft(row, row.Archetype.Name, 1, IsExistingWarband));
            else row.HenchmanGroupDrafts[^1].Count++;
            UpdateRecruitability();
        }

        /// <summary>Cas nouvelle recrue de cette session (dernier slot/groupe pas encore backé par un
        /// Warrior réel) : comportement d'origine, aucune confirmation - rien n'est encore en base.
        /// Cas slot déjà existant (bande rouverte pour édition) et la réduction va passer sous son
        /// effectif réellement recruté (BaselineHeadCount) : demande confirmation (remboursement au Save,
        /// pas avant) - retour utilisateur explicite, décrémenter sous l'effectif existant supprime
        /// réellement le guerrier plutôt que d'être bloqué.</summary>
        [RelayCommand]
        private async Task DecrementWarrior(WarriorRecruitRow row)
        {
            if (row.Count == 0) return;

            if (row.IsHero && row.NameSlots.Count > 0)
            {
                var last = row.NameSlots[^1];
                if (last.ExistingWarrior is { } existingHero)
                {
                    if (!await ConfirmAsync(Loc["DialogDelete"], string.Format(Loc["WarriorDeleteConfirm"], last.DisplayLabel)))
                        return;
                    _pendingFullDeletions.Add(existingHero);
                }
                row.Count--;
                row.NameSlots.RemoveAt(row.NameSlots.Count - 1);
                RenumberHeroLabels(row);
            }
            else if (!row.IsHero && row.HenchmanGroupDrafts.Count > 0)
            {
                var last = row.HenchmanGroupDrafts[^1];
                if (last.ExistingWarrior is not null && last.Count <= last.BaselineHeadCount)
                {
                    if (!await ConfirmAsync(Loc["DialogDelete"], string.Format(Loc["WarriorDeleteConfirm"], last.Name)))
                        return;
                }
                row.Count--;
                last.Count--;
                if (last.Count <= 0)
                {
                    if (last.ExistingWarrior is { } existingGroup) _pendingFullDeletions.Add(existingGroup);
                    row.HenchmanGroupDrafts.RemoveAt(row.HenchmanGroupDrafts.Count - 1);
                }
            }
            else
            {
                row.Count--;
            }
            UpdateRecruitability();
        }

        /// <summary>Sous-ensemble de HiredSwordRows effectivement recruté - alimente le ChipListView de
        /// l'étape Mercenaires ET la 3e section de l'étape Noms (voir WarbandEditDialog.xaml). Recalculée
        /// à chaque tap Add/Remove (voir UpdateRecruitability), même idiome que RecruitedRows.</summary>
        public IEnumerable<HiredSwordRecruitRow> RecruitedHiredSwordRows => HiredSwordRows.Where(r => r.IsRecruited);

        public bool HasRecruitedHiredSwords => RecruitedHiredSwordRows.Any();

        /// <summary>Ouvre IHiredSwordPickerService (ChipListView.AddCommand) - catalogue déjà filtré par
        /// éligibilité à CETTE bande et par "pas déjà recruté" (ExcludedHiredSwordIds), donc "un seul de
        /// chaque type" (livre des règles) n'a même pas besoin d'être revérifié ici. Seule la trésorerie
        /// reste à checker (sauf en Mode Libre, comme IncrementWarrior) - s'arrête au premier
        /// inabordable plutôt que de tout annuler, même idiome qu'AddEquipment.</summary>
        [RelayCommand]
        private async Task AddHiredSword()
        {
            if (Archetype is null) return;

            var excludedIds = HiredSwordRows.Where(r => r.IsRecruited).Select(r => r.HiredSword.Id).ToList();
            var picked = await _hiredSwordPicker.PickHiredSwordsAsync(Archetype.Id, excludedIds);
            foreach (var hiredSword in picked)
            {
                var row = HiredSwordRows.FirstOrDefault(r => r.HiredSword.Id == hiredSword.Id);
                if (row is null || row.IsRecruited) continue;

                if (!IsExistingWarband && !RecruitmentRules.CanRecruitHiredSword(alreadyHasThisType: false, RemainingTreasury, row.Cost))
                {
                    await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                    break;
                }
                row.IsRecruited = true;
            }

            UpdateRecruitability();
        }

        /// <summary>Décocher un Franc-Tireur déjà en base (ChipListView.RemoveCommand) ne fait rien
        /// d'autre que marquer la ligne - le remboursement (TotalRefunds) et la suppression réelle
        /// (DeleteWarriorAsync) n'ont lieu qu'au Save(), sans confirmation (voir HiredSwordRecruitRow).</summary>
        [RelayCommand]
        private void RemoveHiredSword(HiredSwordRecruitRow row)
        {
            row.IsRecruited = false;
            UpdateRecruitability();
        }

        [RelayCommand]
        private Task ShowHiredSwordDetail(HiredSwordRecruitRow row) => _detailDialogs.ShowHiredSwordDetailDialogAsync(row.HiredSword);

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
            var newGroup = new HenchmanGroupDraft(group.Row, group.Row.Archetype.Name, moved, group.IsExistingWarband);
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
            OnPropertyChanged(nameof(RecruitedHiredSwordRows));
            OnPropertyChanged(nameof(HasRecruitedHiredSwords));
            // Le nombre d'étapes suit HasRecruitedHiredSwords (voir StepCount) - dernier Franc-Tireur retiré
            // alors qu'on est sur l'étape de leurs noms : on retombe sur Mercenaires.
            if (SelectedTab >= StepCount) SelectedTab = StepCount - 1;
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(StepLabel));
            OnPropertyChanged(nameof(IsHiredSwordNamesTab));
            OnPropertyChanged(nameof(IsRecapTab));
            // Une erreur de l'étape Équipement (fonds, mutation) se réévalue dès que les achats changent.
            if (EquipmentError is not null) ValidateEquipmentStep();

            if (Archetype is null) return;
            var total = TotalWarriorCount;
            foreach (var row in RecruitRows)
                row.CanIncrement = RecruitmentRules.CanRecruit(row.Count, row.Archetype.MaxCount, total,
                    Archetype.MaxWarriors, IsExistingWarband, RemainingTreasury, row.Cost);
        }

        /// <summary>Achat d'équipement pour une cible : un HenchmanGroupDraft (un seul achat, appliqué à tout le
        /// sous-groupe) ou un WarriorNameSlot (Héros - propre à cette recrue) - pas encore de WarriorId réel
        /// pour appeler AddWarriorEquipmentAsync, donc on garde des EquipmentPick en mémoire jusqu'au Save.
        /// Aucun dialog bloquant après le sélecteur en mode Coûts appliqués (retour utilisateur 2026-09-25,
        /// un dialog ouvert juste après le sélecteur cassait la pile modale) : les fonds insuffisants sont
        /// signalés sur l'étape (ValidateEquipmentStep) et la limite d'armes au clic sur Suivant
        /// (WarnWeaponLimitsAsync), comme dans le wizard Fin de Partie. Seul le mode Libre ouvre
        /// MaterialPickerDialog, et seulement si le lot contient une arme de corps à corps.</summary>
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

            // Coûts appliqués : liste d'équipement du guerrier uniquement (livre - une bande en création
            // n'achète que dans sa liste, rien du Trading Post hors liste). Mode Libre (bande déjà jouée
            // sur papier) : tout le catalogue, objets rares/artefacts trouvés en campagne compris.
            var items = await _equipmentPicker.PickEquipmentAsync(Archetype.Id, row.Archetype.EquipmentListId, row.Archetype.Id, RemainingTreasury, perUnitCost,
                destination.Any(p => p.Item.IsFreeDagger), equipmentListOnly: !IsExistingWarband, unrestricted: IsExistingWarband);
            if (items.Count == 0) return;

            // Matériau (corps à corps) et bénédiction (toute arme) - Tout normal / tap à l'extérieur du
            // dialog = ni l'un ni l'autre, voir MaterialPickerDialogViewModel.ResolveChoices.
            List<MaterialChoice> choices = [];
            bool? confirmed = null;
            if (IsExistingWarband)
            {
                choices = await MaterialPickerDialogViewModel.BuildChoicesAsync(_libraryService, items, destination.Any(p => p.Item.IsFreeDagger));
                if (choices.Count > 0)
                    confirmed = await ShowDialogAsync(new MaterialPickerDialog(new MaterialPickerDialogViewModel(choices, _detailDialogs)));
            }
            var resolved = MaterialPickerDialogViewModel.ResolveChoices(items, choices, confirmed);

            for (var i = 0; i < items.Count; i++)
            {
                var equipmentItem = items[i];
                var (materialRule, blessingRule) = resolved[i];
                destination.Add(new EquipmentPick(equipmentItem, materialRule)
                {
                    BlessingRule = blessingRule,
                    // La première dague est gratuite par guerrier/groupe, uniquement sans matériau (livre des
                    // règles : "in addition to his free dagger") - une deuxième dague, ou un matériau
                    // délibérément choisi sur celle-ci, coûte le prix normal et compte dans la limite
                    // d'armes (voir EquipmentItem.IsFreeDagger/WeaponLimits).
                    IsFree = EquipmentPricing.IsFreeDaggerEligible(equipmentItem.IsFreeDagger, destination.Any(p => p.Item.IsFreeDagger)) && materialRule is null
                });
            }

            UpdateRecruitability();
        }

        /// <summary>Avertissement non-bloquant (2 armes de corps à corps / 2 armes de tir différentes max par
        /// guerrier, livre des règles "Starting a Warband") - certaines règles spéciales de bande (ex. Combat
        /// de Queue Skaven) autorisent à dépasser, donc jamais bloquant - voir WeaponLimits. Déclenché au clic
        /// sur Suivant (ou Enregistrer hors assistant) plutôt qu'à la sélection, comme
        /// EndOfGamePageViewModel.ShowWeaponLimitWarningIfNeededAsync : un dialog ouvert juste après le
        /// sélecteur cassait la pile modale. Seulement pour les cibles ayant reçu au moins un nouvel achat -
        /// un guerrier déjà en base resté tel quel n'est pas réaverti.</summary>
        private async Task WarnWeaponLimitsAsync()
        {
            foreach (var row in RecruitRows)
            {
                var slots = row.NameSlots.Select(s => (Slot: (RecruitSlot)s, Label: s.DisplayLabel))
                    .Concat(row.HenchmanGroupDrafts.Select(g => (Slot: (RecruitSlot)g, Label: g.Name)));
                foreach (var (slot, label) in slots)
                {
                    if (slot.Equipment.All(p => p.ExistingId is not null)) continue;
                    if (WeaponLimits.ExceedsLimits(slot.Equipment.Select(p => p.Item)))
                        await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], label));
                }
            }
        }

        /// <summary>Tap sur un chip d'équipement acheté (groupe ou slot Héros) - même recap qu'ailleurs
        /// dans l'app (EquipmentListEditDialogViewModel.ShowItemDetail, EquipmentItemViewModel.ShowDetails),
        /// pas juste le mini-popup Nom+Description générique (ChipDetailDialog) : un objet d'équipement a
        /// coût/rareté/restrictions propres, pas seulement une description.</summary>
        [RelayCommand]
        private Task ShowEquipmentDetail(EquipmentPick pick) => _detailDialogs.ShowEquipmentDetailDialogAsync(pick.Item, pick.MaterialRule, blessingRule: pick.BlessingRule);

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

        /// <summary>Sous-onglet Mutations (Possédés/Mutants, RecruitSlot.CanBuyMutations) - les deux modes :
        /// livre, "90 gold crowns to hire (+ the cost of mutations)", donc facturé au recrutement en mode Coûts
        /// appliqués (TotalSpent), en mémoire jusqu'au Save. Même cible/idiome qu'AddSkill ; catalogue
        /// restreint à cette bande (Bénédictions de Nurgle réservées aux Impurs, etc.).</summary>
        [RelayCommand]
        private async Task AddMutation(object target)
        {
            ObservableCollection<Mutation> destination;
            switch (target)
            {
                case WarriorNameSlot slot:
                    destination = slot.Mutations;
                    break;
                case HenchmanGroupDraft group:
                    destination = group.Mutations;
                    break;
                default:
                    return;
            }
            if (Archetype is null) return;

            var unitCount = target is HenchmanGroupDraft henchmen ? henchmen.Count : 1;
            var mutations = await _mutationPicker.PickMutationsAsync(Archetype.Id, RemainingTreasury, unitCount);
            foreach (var mutation in mutations)
                destination.Add(mutation);
            UpdateRecruitability();
        }

        [RelayCommand]
        private Task ShowMutationDetail(Mutation mutation) => _detailDialogs.ShowMutationDetailDialogAsync(mutation);

        /// <summary>Retire une mutation de quelle que collection la contient - même idiome que RemoveSkill.</summary>
        [RelayCommand]
        private void RemoveMutation(Mutation mutation)
        {
            foreach (var row in RecruitRows)
            {
                foreach (var group in row.HenchmanGroupDrafts)
                {
                    if (group.Mutations.Remove(mutation)) { UpdateRecruitability(); return; }
                }
                foreach (var slot in row.NameSlots)
                {
                    if (slot.Mutations.Remove(mutation)) { UpdateRecruitability(); return; }
                }
            }
        }

        /// <summary>Sous-onglet Blessures (Héros, mode Bande existante - RecruitSlot.ShowInjuriesTab) : blessures
        /// déjà subies par un guerrier importé d'une bande jouée sur papier, en mémoire jusqu'au Save où leur
        /// malus permanent s'applique au profil (ApplyInjuryPenalties).</summary>
        [RelayCommand]
        private async Task AddInjury(object target)
        {
            if (target is not RecruitSlot slot) return;
            foreach (var injury in await _injuryPicker.PickInjuriesAsync())
                slot.Injuries.Add(injury);
        }

        [RelayCommand]
        private Task ShowInjuryDetail(Injury injury) => _detailDialogs.ShowInjuryDetailDialogAsync(injury);

        /// <summary>Retire une blessure de quelle que collection la contient - même idiome que RemoveSkill.</summary>
        [RelayCommand]
        private void RemoveInjury(Injury injury)
        {
            foreach (var slot in RecruitRows.SelectMany(r => r.NameSlots.Cast<RecruitSlot>().Concat(r.HenchmanGroupDrafts)))
                if (slot.Injuries.Remove(injury)) return;
        }

        /// <summary>Malus permanent de chaque blessure (Core.Rules.SeriousInjuryEffectTable.TryGetPermanentPenalty
        /// - Jambe blessée -1 M, Main blessée -1 CC...) appliqué au profil du guerrier : direction -1 pour une
        /// blessure ajoutée, +1 pour une blessure déjà en base retirée (annule son malus). Renvoie true si le
        /// profil a changé (à sauvegarder).</summary>
        private static bool ApplyInjuryPenalties(Warrior warrior, IEnumerable<Injury> injuries, int direction)
        {
            var changed = false;
            foreach (var injury in injuries)
            {
                if (!SeriousInjuryEffectTable.TryGetPermanentPenalty(injury.Category, injury.RollRange, out var field)) continue;
                CharacteristicModifier.Apply(warrior, field, direction);
                changed = true;
            }
            return changed;
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
        /// historique déjà déterminé, pas faire un nouveau tirage). Ouvre SpellRollDialog (contexte école
        /// de magie + saisie du jet, résolution et récap à la demande gérés dans le dialog lui-même) -
        /// n'ajoute le sort que si le joueur ferme via Accept (résultat non-null), Annuler renvoie null.</summary>
        [RelayCommand]
        private async Task ShowSpellRollDialog(object target)
        {
            RecruitSlot? slot = target switch
            {
                WarriorNameSlot s => s,
                HenchmanGroupDraft g => g,
                _ => null
            };
            if (slot is null || Archetype is null) return;

            var knownSpellIds = slot.Spells.Select(s => s.Id).ToList();
            var spell = await ShowDialogAsync(new SpellRollDialog(new SpellRollDialogViewModel(Archetype.MagicSchools, _libraryService, _detailDialogs, knownSpellIds)));
            if (spell is null) return;

            slot.Spells.Add(spell);
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
        /// MinCount). Les noms se valident séparément, à l'étape Noms (voir ValidateWarriorNamesStep).</summary>
        private bool ValidateWarriorsStep()
        {
            if (Archetype is null) return false;

            if (!RecruitmentRules.MeetsMinWarriors(TotalWarriorCount, Archetype.MinWarriors))
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

        /// <summary>Étape Équipement : chaque NOUVELLE recrue d'un type à mutation obligatoire (Mutant -
        /// RecruitmentRules.IsMissingMandatoryMutation) doit en avoir au moins une. Mode Coûts appliqués
        /// uniquement (le mode Libre enregistre un historique tel quel) et jamais pour un guerrier déjà en
        /// base (ExistingWarrior) - une bande recrutée avant cette règle ne doit pas se retrouver bloquée
        /// à l'enregistrement.</summary>
        private bool ValidateEquipmentStep()
        {
            if (!IsExistingWarband)
            {
                // Fonds insuffisants signalés ici plutôt que par un dialog juste après le sélecteur
                // d'équipement (voir AddEquipment) - le sélecteur affiche déjà le restant en direct.
                if (RemainingTreasury < 0)
                {
                    EquipmentError = Loc["WarbandsInsufficientFundsMessage"];
                    return false;
                }

                foreach (var row in RecruitRows.Where(r => r.Archetype.MustStartWithMutation))
                {
                    var slots = row.NameSlots.Select(s => (Slot: (RecruitSlot)s, Label: s.DisplayLabel))
                        .Concat(row.HenchmanGroupDrafts.Select(g => (Slot: (RecruitSlot)g, Label: g.Name)));
                    foreach (var (slot, label) in slots)
                    {
                        if (slot.ExistingWarrior is null && RecruitmentRules.IsMissingMandatoryMutation(true, slot.Mutations.Count))
                        {
                            EquipmentError = string.Format(Loc["WarbandsMandatoryMutationMissing"], label);
                            return false;
                        }
                    }
                }
            }

            EquipmentError = null;
            return true;
        }

        /// <summary>Étape Noms (Héros/Hommes de main) : un nom renseigné pour chaque recrue Héros et
        /// chaque sous-groupe d'Hommes de main (livre des règles : "you will need to... name each
        /// Henchman group") - PopulateSuggestedNames pré-remplit déjà tout à l'entrée de cette étape,
        /// donc ce garde-fou ne mord que si le joueur a vidé un champ après coup. Les Francs-Tireurs ont
        /// leur propre étape/validation séparée depuis le 2026-09-01 - voir ValidateHiredSwordNamesStep.</summary>
        private bool ValidateWarriorNamesStep()
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

        /// <summary>Étape Noms (Francs-Tireurs) : même exigence qu'un Héros, une identité propre pour
        /// chaque Franc-Tireur recruté à l'étape Mercenaires (IsMercenariesTab/RecruitedHiredSwordRows) -
        /// dans sa propre étape/validation depuis le 2026-09-01 (retour utilisateur), auparavant une 3e
        /// section de ValidateNamesStep partagée avec les Héros/Hommes de main.</summary>
        private bool ValidateHiredSwordNamesStep()
        {
            foreach (var row in HiredSwordRows.Where(r => r.IsRecruited))
            {
                if (string.IsNullOrWhiteSpace(row.Name))
                {
                    HiredSwordNamesError = string.Format(Loc["WarbandsWarriorNameRequired"], row.HiredSword.Name);
                    return false;
                }
            }

            HiredSwordNamesError = null;
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
        /// Équipement→Noms→Mercenaires→Noms. Validé par étape : en quittant Général, bloque tant que
        /// Nom/Archetype ne sont pas renseignés ; en quittant Guerriers, bloque tant que
        /// ValidateWarriorsStep échoue (effectif minimum) ; en quittant la 1re étape Noms, bloque tant que
        /// ValidateWarriorNamesStep échoue - contrairement à une passe précédente (une seule étape Noms,
        /// toujours en dernier), celle-ci n'est plus la dernière étape donc a besoin de son propre garde-fou
        /// ici, pas seulement au Save(). La 2e étape Noms (Francs-Tireurs) reste sans garde-fou dans Next -
        /// elle EST la dernière étape, comme l'ancienne étape Noms unique, validée au Save().</summary>
        [RelayCommand]
        private async Task Next()
        {
            if (IsGeneralTab && !ValidateGeneralStep()) return;
            if (IsWarriorsTab && !ValidateWarriorsStep()) return;
            if (IsEquipmentTab && !ValidateEquipmentStep()) return;
            if (IsEquipmentTab) await WarnWeaponLimitsAsync();
            if (IsWarriorNamesTab && !ValidateWarriorNamesStep()) return;
            // Plus la dernière étape en création pas à pas (Récapitulatif après, voir StepCount) - validée ici
            // plutôt que seulement au Save.
            if (IsHiredSwordNamesTab && !ValidateHiredSwordNamesStep()) return;
            if (SelectedTab >= StepCount - 1) return;
            SelectedTab++;

            if (IsWarriorsTab || IsEquipmentTab || IsMercenariesTab) await EnsureRecruitableArchetypesLoadedAsync();
            if (IsWarriorNamesTab) PopulateSuggestedNames();
        }

        /// <summary>Mode assistant uniquement (bouton Précédent).</summary>
        [RelayCommand]
        private void Back()
        {
            if (SelectedTab > 0) SelectedTab--;
        }

        /// <summary>Synchronise un slot backé par un Warrior déjà en base (bande rouverte pour édition) -
        /// diff Équipement/Compétences/Sorts contre la baseline chargée à l'ouverture (voir RecruitSlot.
        /// BaselineEquipment/BaselineSkills/BaselineSpells) plutôt que de tout recréer : un pick sans
        /// ExistingId est un nouvel achat (AddWarriorEquipmentAsync), une entrée de baseline absente de la
        /// collection courante a été retirée (RemoveWarriorEquipmentAsync) - même identité d'objet pour
        /// Compétences/Sorts (voir RecruitSlot constructeur, Skills/Spells portent directement Item).</summary>
        /// <summary>Persiste un EquipmentPick sur un guerrier en base - matériau à l'insertion, bénédiction
        /// ensuite (AddWarriorEquipmentAsync n'a pas de paramètre bénédiction, même idiome que
        /// EndOfGamePageViewModel.Apply.Recruitment).</summary>
        private async Task AddPickAsync(int warriorId, EquipmentPick pick)
        {
            var carried = await _warbandService.AddWarriorEquipmentAsync(warriorId, pick.Item, materialRule: pick.MaterialRule);
            if (pick.BlessingRule is { } blessing)
                await _warbandService.SetWarriorEquipmentBlessingRuleAsync(carried.Id, blessing.Id);
        }

        private async Task SyncExistingSlotAsync(Warrior w, RecruitSlot slot, string name, int? newHeadCount)
        {
            var dirty = false;
            if (w.Name != name) { w.Name = name; dirty = true; }
            if (newHeadCount is { } headCount && w.HeadCount != headCount) { w.HeadCount = headCount; dirty = true; }
            if (IsExistingWarband && w.Experience != slot.Experience) { w.Experience = slot.Experience; dirty = true; }
            if (dirty) await _warbandService.SaveWarriorAsync(w);

            foreach (var pick in slot.Equipment.Where(p => p.ExistingId is null))
                await AddPickAsync(w.Id, pick);
            foreach (var baseline in slot.BaselineEquipment.Where(b => slot.Equipment.All(p => p.ExistingId != b.Id)))
                await _warbandService.RemoveWarriorEquipmentAsync(baseline.Id);

            foreach (var skill in slot.Skills.Where(s => slot.BaselineSkills.All(b => b.Item != s)))
                await _warbandService.AddWarriorSkillAsync(w.Id, skill);
            foreach (var baseline in slot.BaselineSkills.Where(b => !slot.Skills.Contains(b.Item)))
                await _warbandService.RemoveWarriorSkillAsync(baseline.Id);

            foreach (var spell in slot.Spells.Where(s => slot.BaselineSpells.All(b => b.Item != s)))
                await _warbandService.AddWarriorSpellAsync(w.Id, spell);
            foreach (var baseline in slot.BaselineSpells.Where(b => !slot.Spells.Contains(b.Item)))
                await _warbandService.RemoveWarriorSpellAsync(baseline.Id);

            foreach (var mutation in slot.Mutations.Where(m => slot.BaselineMutations.All(b => b.Item != m)))
                await _warbandService.AddWarriorMutationAsync(w.Id, mutation);
            foreach (var baseline in slot.BaselineMutations.Where(b => !slot.Mutations.Contains(b.Item)))
                await _warbandService.RemoveWarriorMutationAsync(baseline.Id);

            // Blessures (mode Bande existante) : ajoutées -> malus appliqué, retirées -> malus annulé.
            var addedInjuries = slot.Injuries.Where(i => slot.BaselineInjuries.All(b => b.Item != i)).ToList();
            var removedInjuries = slot.BaselineInjuries.Where(b => !slot.Injuries.Contains(b.Item)).ToList();
            foreach (var injury in addedInjuries)
                await _warbandService.AddWarriorInjuryAsync(w.Id, injury);
            foreach (var baseline in removedInjuries)
                await _warbandService.RemoveWarriorInjuryAsync(baseline.Id);
            var penaltiesChanged = ApplyInjuryPenalties(w, addedInjuries, direction: -1);
            penaltiesChanged |= ApplyInjuryPenalties(w, removedInjuries.Select(b => b.Item), direction: 1);
            if (penaltiesChanged) await _warbandService.SaveWarriorAsync(w);
        }

        /// <summary>Point d'écriture en base pour la bande, les NOUVELLES recrues de cette session
        /// (RecruitWarriorAsync) ET la synchronisation du roster déjà existant (SyncExistingSlotAsync) -
        /// unifiés dans le même Save() plutôt qu'un panier différé + une persistance immédiate séparée
        /// (retour utilisateur explicite : même wizard, tout différé jusqu'à Enregistrer, y compris les
        /// changements sur des guerriers déjà en base). Les suppressions confirmées pendant la session
        /// (DecrementWarrior sous l'effectif existant) ne s'exécutent qu'ici aussi (_pendingFullDeletions),
        /// avec leur remboursement.</summary>
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

            if (!ValidateEquipmentStep())
            {
                SelectedTab = 2;
                return;
            }
            // Hors assistant, pas de clic sur Suivant pour porter l'avertissement - voir WarnWeaponLimitsAsync.
            if (!IsWizardMode) await WarnWeaponLimitsAsync();

            PopulateSuggestedNames();
            if (!ValidateWarriorNamesStep())
            {
                SelectedTab = WarriorNamesTabIndex;
                return;
            }

            if (!ValidateHiredSwordNamesStep())
            {
                SelectedTab = HiredSwordNamesTabIndex;
                return;
            }

            await Loading.RunAsync(async () =>
            {
                // TotalRefunds capturé avant que la boucle ci-dessous ne synchronise le roster existant -
                // même valeur affichée en direct pendant la session (RemainingTreasury), seulement
                // pertinent en mode Coûts appliqués (ignoré en mode Libre où TreasuryOverride prime).
                var refunds = TotalRefunds;

                int warbandId;
                if (Item.Id == 0)
                {
                    var created = await _warbandService.CreateWarbandAsync(Item.Name, Archetype!);
                    warbandId = created.Id;
                    created.Treasury = IsExistingWarband ? TreasuryOverride : Archetype!.StartingTreasury - TotalSpent;
                    // Mode Libre uniquement (saisie de l'étape Général) - une bande neuve démarre sans pierre.
                    created.WyrdstoneShards = IsExistingWarband ? Math.Max(0, Item.WyrdstoneShards) : 0;
                    await _warbandService.SaveWarbandAsync(created);
                }
                else
                {
                    warbandId = Item.Id;
                    Item.Treasury = IsExistingWarband ? TreasuryOverride : RosterStartingTreasury - TotalSpent + refunds;
                    Item.WyrdstoneShards = Math.Max(0, Item.WyrdstoneShards);
                    await _warbandService.SaveWarbandAsync(Item);
                }

                foreach (var row in RecruitRows.Where(r => r.Count > 0))
                {
                    if (row.IsHero)
                    {
                        foreach (var slot in row.NameSlots)
                        {
                            if (slot.ExistingWarrior is { } existingHero)
                            {
                                await SyncExistingSlotAsync(existingHero, slot, slot.Name.Trim(), newHeadCount: null);
                                continue;
                            }

                            var warrior = await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, slot.Name.Trim());
                            foreach (var pick in slot.Equipment)
                                await AddPickAsync(warrior.Id, pick);
                            foreach (var skill in slot.Skills)
                                await _warbandService.AddWarriorSkillAsync(warrior.Id, skill);
                            foreach (var spell in slot.Spells)
                                await _warbandService.AddWarriorSpellAsync(warrior.Id, spell);
                            foreach (var mutation in slot.Mutations)
                                await _warbandService.AddWarriorMutationAsync(warrior.Id, mutation);
                            foreach (var injury in slot.Injuries)
                                await _warbandService.AddWarriorInjuryAsync(warrior.Id, injury);
                            var profileChanged = ApplyInjuryPenalties(warrior, slot.Injuries, direction: -1);
                            if (IsExistingWarband && slot.Experience != warrior.Experience)
                            {
                                warrior.Experience = slot.Experience;
                                profileChanged = true;
                            }
                            if (profileChanged) await _warbandService.SaveWarriorAsync(warrior);
                        }
                    }
                    else
                    {
                        // Un seul Warrior par groupe (HeadCount = l'effectif) plutôt qu'une ligne par
                        // individu - le groupe est mécaniquement une seule entité (livre des règles :
                        // XP/équipement/compétences partagés par tout le groupe), voir Warrior.HeadCount.
                        foreach (var group in row.HenchmanGroupDrafts)
                        {
                            if (group.ExistingWarrior is { } existingGroup)
                            {
                                await SyncExistingSlotAsync(existingGroup, group, group.Name.Trim(), newHeadCount: group.Count);
                                continue;
                            }

                            var warrior = await _warbandService.RecruitWarriorAsync(warbandId, row.Archetype, group.Name.Trim(), headCount: group.Count);
                            foreach (var pick in group.Equipment)
                                await AddPickAsync(warrior.Id, pick);
                            foreach (var skill in group.Skills)
                                await _warbandService.AddWarriorSkillAsync(warrior.Id, skill);
                            foreach (var spell in group.Spells)
                                await _warbandService.AddWarriorSpellAsync(warrior.Id, spell);
                            foreach (var mutation in group.Mutations)
                                await _warbandService.AddWarriorMutationAsync(warrior.Id, mutation);
                            if (IsExistingWarband && group.Experience != warrior.Experience)
                            {
                                warrior.Experience = group.Experience;
                                await _warbandService.SaveWarriorAsync(warrior);
                            }
                        }
                    }
                }

                foreach (var warrior in _pendingFullDeletions)
                    await _warbandService.DeleteWarriorAsync(warrior.Id);

                // Francs-Tireurs : équipement de départ résolu une seule fois (fixe, voir HiredSword.
                // StartingEquipmentIds) - seulement si au moins une NOUVELLE recrue en a besoin.
                var hiredSwordEquipmentCatalog = HiredSwordRows.Any(r => r.IsRecruited && r.ExistingWarrior is null)
                    ? await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language)
                    : new List<EquipmentItem>();

                foreach (var row in HiredSwordRows)
                {
                    if (row.IsRecruited)
                    {
                        if (row.ExistingWarrior is { } existing)
                        {
                            var trimmedName = row.Name.Trim();
                            if (existing.Name != trimmedName)
                            {
                                existing.Name = trimmedName;
                                await _warbandService.SaveWarriorAsync(existing);
                            }
                        }
                        else
                        {
                            var startingEquipment = hiredSwordEquipmentCatalog.Where(e => row.HiredSword.StartingEquipmentIds.Contains(e.Id)).ToList();
                            await _warbandService.RecruitHiredSwordAsync(warbandId, row.HiredSword, row.Name.Trim(), startingEquipment);
                        }
                    }
                    else if (row.ExistingWarrior is { } departed)
                    {
                        await _warbandService.DeleteWarriorAsync(departed.Id);
                    }
                }
            });

            Close(true);
        }
    }
}
