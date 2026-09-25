using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>
/// Wizard qui suit la "Séquence d'après-bataille" du livre de règles : Résultat, Hors de combat,
/// une étape Blessure par guerrier coché hors de combat, Expérience, Progression, Exploration
/// (chapitre "Revenus" - jet + résolution de la table d'Exploration, voir Core.Rules.ExplorationChart
/// et Models.Library.ExplorationResult/ExplorationOutcome), Récapitulatif - dans cet ordre (Blessures
/// Graves puis Expérience puis Revenus, à faire "devant témoin" juste après la partie ; le reste de la
/// séquence - vente de pierre magique, disponibilité des vétérans, personnages spéciaux, achats...
/// - n'est pas dans ce dialog, soit hors périmètre de cette passe (voir le plan de séquencement) soit
/// déjà couvert ailleurs dans l'appli, ex. Recruter/Ajouter un objet sur la carte guerrier).
/// Résultat n'est pas une étape du livre à proprement parler, gardé en premier comme contexte léger
/// pour la phrase d'Historique.
///
/// Steps est reconstruite à chaque accès plutôt que mise en cache (mêmes bases que WarriorRows,
/// jamais réaffectée) : le nombre d'étapes Blessure dépend de IsOutOfAction et le nombre d'étapes
/// Progression de HasMilestone - une "carte pleine écran" par guerrier concerné dans les deux cas
/// (décision explicite du 2026-08-17 pour ne pas surcharger un seul écran avec tous les guerriers
/// concernés à la fois - Hors de combat/Expérience restent des vues d'ensemble, ce sont Blessure et
/// Progression qui se découpent guerrier par guerrier). Décocher un Héros sur l'étape "Hors de combat"
/// fait disparaître son étape Blessure ET efface tout ce qui y avait été saisi
/// (WarriorOutcomeRow.OnOutOfActionCountChanged) - il n'y a plus de blessure à montrer. Le Statut n'est
/// plus une saisie manuelle du tout - voir WarriorOutcomeRow.ApplyInjuryRoll.
///
/// Un groupe d'Hommes de main (HeadCount potentiellement &gt; 1) n'a pas une simple case à cocher mais
/// un stepper "combien de figurines hors de combat" (IncrementOutOfAction/DecrementOutOfAction) - la
/// règle du livre veut un jet de Blessure Grave par figurine concernée, pas un seul jet pour tout le
/// groupe (confirmé par l'utilisateur, 2026-08-17). Son étape Blessure affiche alors autant de jets D6
/// indépendants que de figurines indiquées (WarriorOutcomeRow.FigureInjuryRolls), chacun pouvant tuer sa
/// figurine sans affecter les autres - WarbandDetailViewModel.EndOfGame décompte les morts pour
/// décrémenter Warrior.HeadCount à l'enregistrement (jamais pendant le wizard lui-même).
/// </summary>
[QueryProperty(nameof(WarbandId), "warbandId")]
public partial class EndOfGamePageViewModel : BaseViewModel
{
    private readonly ISkillPickerService _skillPicker;
    private readonly IMutationPickerService _mutationPicker;
    private readonly IDetailDialogService _detailDialogs;
    private readonly ILibraryService _libraryService;
    private readonly IWarbandService _warbandService;
    private readonly IHiredSwordPickerService _hiredSwordPicker;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISellEquipmentPickerService _sellEquipmentPicker;
    private readonly IDramatisPersonaPickerService _dramatisPersonaPicker;
    private int _warbandArchetypeId;

    /// <summary>EquipmentItem ids the warband's unassigned inventory currently owns at least one of -
    /// passed straight through to each RareItemSearchEntry (see its own doc) to decide whether an
    /// alternative-payment option (e.g. Johann/Crimson Shade) can even be offered.</summary>
    private IReadOnlyCollection<int> _ownedEquipmentItemIds = new List<int>();

    /// <summary>DramatisPersona ids currently on cooldown for this warband (2026-09-01, délai de
    /// re-recherche - voir DramatisPersona.RequiresCooldownBeforeResearch) - passed to
    /// IDramatisPersonaPickerService.PickDramatisPersonaAsync so the "Personnage spécial" search picker
    /// never even shows them as an option.</summary>
    private IReadOnlyCollection<int> _cooldownDramatisPersonaIds = new List<int>();

    /// <summary>English WarbandArchetype.Name of the warband playing this game (e.g. "Skaven of Clan
    /// Eshin") - needed alongside _warbandArchetypeId because a Groupe B "conditional on warband type"
    /// Exploration branch (Core.Rules.ExplorationOutcomeResolver.ResolveWarbandOutcome) matches by name,
    /// not Id (see ExplorationOutcome.RestrictedToWarbandArchetypeNames - a plain string reference, same
    /// idiom as EquipmentItemName, since this is fixed rulebook content with no editor).</summary>
    private string _warbandArchetypeName = string.Empty;

    /// <summary>Captured once at dialog construction (see Warband.PendingExplorationBonusDie) - read by
    /// ExplorationDiceCount, never reassigned mid-wizard: the flag itself is only cleared on the Warband
    /// once this Fin de Partie is actually saved (WarbandDetailViewModel.EndOfGame).</summary>
    private bool _pendingExplorationBonusDie;

    /// <summary>Captured once at dialog construction (see Warband.HasCatacombReroll) - permanent, unlike
    /// _pendingExplorationBonusDie, so it's only ever read to show an informational reminder in the
    /// Exploration roll step (ShowCatacombRerollReminder), never cleared/consumed.</summary>
    private bool _hasCatacombReroll;

    /// <summary>Snapshot of Warband.Treasury at dialog-open time (see EquippedHenchmanTreasuryAfter,
    /// Prisonniers' "autres bandes" branch) - the wizard never writes to the real Warband mid-dialog
    /// (only WarbandDetailViewModel.EndOfGame does, at Save), so this stays a plain frozen number for
    /// the affordability preview rather than a live reference.</summary>
    private int _currentTreasury;

    /// <summary>Snapshot of Warband.WyrdstoneShards at dialog-open time (see _currentTreasury for the
    /// same reasoning) - drives whether the Wyrdstone Sale step even appears (Steps) and bounds
    /// ShardsToSell. Never live-updated mid-wizard: only WarbandDetailViewModel.EndOfGame writes to the
    /// real Warband, at Save.</summary>
    private int _currentWyrdstoneShards;

    private List<ExplorationResult> _explorationResults = new();

    /// <summary>Nom anglais -> EquipmentItem résolu dans la langue courante, pour l'unique champ de ce
    /// wizard qui référence le catalogue Équipement par nom anglais brut plutôt que par Id
    /// (ExplorationOutcome.EquipmentItemName, voir sa doc) - sans ça, "Axe" s'affichait tel quel même en
    /// français. Construit une seule fois par l'appelant (WarbandDetailViewModel.EndOfGame) plutôt que
    /// refait à chaque résolution de branche. L'item entier (pas juste son nom) permet d'afficher un
    /// vrai ChipView tapable (icône de catégorie + popup détail via _detailDialogs) plutôt qu'un simple
    /// Label - même langage d'interaction que le reste de l'app pour toute référence Équipement.</summary>
    private IReadOnlyDictionary<string, EquipmentItem> _equipmentItemsByEnglishName = new Dictionary<string, EquipmentItem>();

    /// <summary>Même idée que _equipmentItemsByEnglishName, pour ExplorationOutcome.MaterialRuleName (ex.
    /// "Ornate Weapon") - permet au ChipView d'afficher "Épée (O)" comme n'importe quel objet en Gromril/
    /// Ithilmar (voir WarbandEquipment.NameDisplay) plutôt que le nom nu de l'item.</summary>
    private IReadOnlyDictionary<string, SpecialRule> _specialRulesByEnglishName = new Dictionary<string, SpecialRule>();

    /// <summary>Catalogues bruts (pas les dictionnaires anglais->résolu ci-dessus) nécessaires au
    /// pipeline Apply*Async (EndOfGamePageViewModel.Apply.cs, voir ApplyExplorationOutcomeAsync/
    /// ApplyRareItemSearchAsync) pour matcher un nom anglais brut (ExplorationOutcome.EquipmentItemName/
    /// MaterialRuleName) contre le catalogue complet, ou pour résoudre un objet localisé à ajouter à
    /// l'inventaire - vivaient comme simples variables locales dans WarbandDetailViewModel.EndOfGame()
    /// avant le passage en page Shell (2026-09-22), promus en champs pour survivre entre InitializeAsync
    /// (où ils sont chargés) et FinishAsync (où le pipeline les consomme).</summary>
    private List<EquipmentItem> _englishEquipmentCatalog = new();
    private List<SpecialRule> _englishSpecialRulesCatalog = new();
    private List<EquipmentItem> _localizedEquipmentCatalog = new();

    /// <summary>Instantané FIGÉ de la réserve à l'ouverture du wizard (une ligne PreExisting par ligne
    /// WarbandEquipment réelle, jamais mutée) - point de départ de BuildReserve, la seule source de vérité
    /// de la réserve (voir EndOfGamePageViewModel.Reserve.cs), et référence du diff d'ApplyReserveAsync à
    /// Terminer. Aussi lu directement par "Céder du matériel" (SeizedEquipmentItems) et le paiement
    /// Dramatis Persona en objet (AlternativePaymentLines), volontairement limités au stock d'avant la
    /// bataille.</summary>
    private List<ReserveLine> _originalReserveSnapshot = new();

    /// <summary>Nom anglais -> WarriorArchetype résolu dans la langue courante, pour
    /// ExplorationOutcome.GrantsFreeHenchmanArchetypeName (ex. "Zombie", Traînard) - même besoin que
    /// _equipmentItemsByEnglishName, mais limité aux archétypes de LA bande jouée (une branche
    /// conditionnée à une bande ne référence jamais l'archétype d'une autre).</summary>
    private IReadOnlyDictionary<string, WarriorArchetype> _warriorArchetypesByEnglishName = new Dictionary<string, WarriorArchetype>();

    /// <summary>Nom anglais -> Id de compétence, pour résoudre EquipmentItem.GrantsSpecificSkillName
    /// (voir Core.Rules.SkillEligibility.EffectiveExtraSkillNames) vers les ids que _skillPicker attend -
    /// le picker travaille sur son propre catalogue localisé, ce dictionnaire ne sert qu'à traverser la
    /// frontière anglais->id une fois, ici, plutôt qu'à chaque PickAdvanceSkill.</summary>
    private IReadOnlyDictionary<string, int> _skillIdsByEnglishName = new Dictionary<string, int>();

    [ObservableProperty]
    private int warbandId;

    public ObservableCollection<string> ResultOptions { get; } = new();

    /// <summary>[ObservableProperty] plutôt qu'un simple { get; private set; } - nécessaire depuis que
    /// cette collection est peuplée dans InitializeAsync (async, après que la page ait déjà posé son
    /// BindingContext) plutôt que dans le constructeur : sans la notification PropertyChanged générée par
    /// [ObservableProperty], BindableLayout.ItemsSource="{Binding WarriorRows}" reste accroché à
    /// l'instance VIDE capturée au tout premier binding et n'apprend jamais qu'une nouvelle instance a été
    /// affectée (bug trouvé le 2026-09-22, retour utilisateur - "pour les blessures, aucun personnage ne
    /// s'affiche"). Même raison que WarbandDetailViewModel.Heroes/Henchmen, déjà [ObservableProperty] pour
    /// exactement ce même besoin.</summary>
    [ObservableProperty]
    private ObservableCollection<WarriorOutcomeRow> warriorRows = new();

    private string selectedResult = string.Empty;

    /// <summary>Backing field manuel plutôt qu'un simple [ObservableProperty] (2026-09-23, diagnostic
    /// utilisateur - "la valeur SelectedResult est remise à null lorsqu'on navigue dans les steps") :
    /// Picker.SelectedItem (ResultStepView.xaml) est lié TwoWay par défaut - quand la vue mise en cache
    /// (StepViewConverter) est rebranchée sur Content après un retour en arrière (Précédent), le Picker
    /// natif WinUI peut brièvement rapporter "aucune sélection" pendant son réattachement, et ce null
    /// remonte alors dans SelectedResult via ce même binding TwoWay, effaçant silencieusement la vraie
    /// valeur déjà choisie - la cause réelle du bug "on perd l'affichage du résultat", pas un simple
    /// défaut de redessin (les tentatives précédentes pour forcer un redessin du Picker ne pouvaient rien
    /// changer : la DONNÉE elle-même était déjà perdue). Le setter ignore désormais toute valeur qui n'est
    /// pas un membre réel de ResultOptions (donc ni null, ni vide) au lieu de l'accepter aveuglément comme
    /// [ObservableProperty] le ferait - seule une vraie sélection du joueur (ou le défaut posé au
    /// constructeur) peut donc changer cette valeur.</summary>
    public string SelectedResult
    {
        get => selectedResult;
        set
        {
            if (!ResultOptions.Contains(value)) return;
            SetProperty(ref selectedResult, value);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStepKind))]
    [NotifyPropertyChangedFor(nameof(CurrentInjuryWarrior))]
    [NotifyPropertyChangedFor(nameof(CurrentPitFightOutcome))]
    [NotifyPropertyChangedFor(nameof(CurrentPitFightSubRoll))]
    [NotifyPropertyChangedFor(nameof(IsPitFightMainOccurrence))]
    [NotifyPropertyChangedFor(nameof(InjuryProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CurrentAdvanceWarrior))]
    [NotifyPropertyChangedFor(nameof(CurrentAdvanceRolls))]
    [NotifyPropertyChangedFor(nameof(AdvanceProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CurrentRecruitHeroSlot))]
    [NotifyPropertyChangedFor(nameof(RecruitHeroProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int stepIndex;

    partial void OnStepIndexChanged(int value)
    {
        if (Current.Kind == StepKind.ExplorationRoll) SyncExplorationDice();
    }

    /// <summary>Public depuis le passage à une vue par étape (2026-09-22, voir Steps/StepViewConverter.cs)
    /// - le convertisseur, hors de cette classe, doit pouvoir lire cette valeur pour choisir la bonne
    /// vue.</summary>
    public enum StepKind { Result, ScenarioRewards, OutOfAction, Injury, PitFight, Captives, Experience, Advance, ExplorationRoll, ExplorationResult, WyrdstoneSale, AvailableVeterans, RareItems, RareItemPurchase, HiredSwords, DramatisPersonae, PairEngagement, PairDuel, DismissWarriors, EquipmentTrading, RecruitHeroesCount, RecruitHeroDetail, RecruitVeteranTopUp, RecruitHenchmenCount, RecruitHenchmenEquipment, RecruitHenchmenNames, EquipmentReallocation, Recap }

    /// <summary>IsExplorationAdvance distingue les DEUX passages possibles par StepKind.Advance pour un
    /// même guerrier : le premier (false), juste après Expérience, pour les paliers franchis par l'XP de
    /// bataille normale (WarriorOutcomeRow.MilestoneCount/AdvanceRolls) - place officielle de la
    /// Progression dans la séquence du livre ; le second (true), juste après Exploration, pour des
    /// paliers UNIQUEMENT atteints grâce à l'XP accordée par la table d'Exploration (Traînard/Prisonniers/
    /// Cimetière - WarriorOutcomeRow.ExplorationMilestoneCount/ExplorationAdvanceRolls), qui ne peut être
    /// détecté qu'une fois cette XP-là connue, donc après l'étape Exploration. Voir CurrentAdvanceRolls.</summary>
    /// <summary>SubRoll (2026-09-04, retour utilisateur - "à chaque blessure on refait un D66 avec le
    /// sous-jet adéquat") distingue, pour un StepKind.PitFight, l'occurrence résolue : null = le jet
    /// principal de Warrior (comportement d'origine) ; non-null = une "Blessures multiples" sous-jet
    /// précis de Warrior.MultipleInjuryRolls (un guerrier peut donc enchaîner plusieurs étapes Vendu aux
    /// Fosses dans la même Fin de Partie, une par occurrence de 65). Voir CurrentPitFightOutcome.</summary>
    private sealed record WizardStep(StepKind Kind, WarriorOutcomeRow? Warrior = null, bool IsExplorationAdvance = false, InjurySubRollEntry? SubRoll = null, WarriorNameSlot? RecruitHeroSlot = null);

    // --- Trésorerie : UNE seule source de vérité pour tout le wizard (2026-09-03, retour utilisateur -
    // "on n'utilise pas le même solde de trésorerie à la corruption et à l'achat d'objet rare... on doit
    // avoir un seul solde de trésorerie qu'on ajuste au fur et à mesure des étapes") ------------------
    //
    // Avant ce correctif, chaque étape calculait sa PROPRE version partielle du solde restant
    // (RareItemPurchaseRemainingTreasury/HiredSwordTreasuryAfter/DramatisPersonaeBaselineTreasury/
    // EquippedHenchmanTreasuryAfter), chacune ne tenant compte QUE de ce qui avait déjà été décidé dans
    // SA PROPRE branche - payer 200 CO de corruption à l'étape Prisonniers (tôt dans le wizard) ne
    // faisait pas baisser le solde affiché ensuite à l'étape Achat d'Objets rares (bien plus tard).
    // Puisque l'addition/soustraction ne dépend pas de l'ORDRE des étapes (juste de CE QUI a été décidé,
    // n'importe où dans ce même wizard), EndOfGameTreasuryRemaining additionne tout net en une seule
    // fois - chaque poste redevient indépendant des autres, aucun besoin de connaître l'ordre réel des
    // étapes pour rester juste.

    /// <summary>Solde de trésorerie PROJETÉ pour cette Fin de Partie - part de la trésorerie de la bande
    /// AVANT cette partie (_currentTreasury), additionne ce que CETTE partie a rapporté (or
    /// d'Exploration, vente de pierres magiques), puis retranche TOUT ce qui a déjà été décidé n'importe
    /// où dans ce même wizard : équipement d'un groupe d'Hommes de main (Exploration), Objets rares
    /// achetés + personnages recrutés à frais en or, engagement d'un nouveau Franc-Tireur + soldes des
    /// Francs-Tireurs déjà engagés, soldes de Dramatis Personae en or (Johann/Veskit/Marianna),
    /// corruption/rétention de Marquand &amp; Ulli. Volontairement absent : les frais en pierre magique
    /// (Nicodemus, RareItemPurchaseTotalWyrdstoneCost) - devise séparée, ne dispute jamais ce solde en
    /// or. Unique consommateur direct des propriétés "Afford"/"Unaffordable" de chaque étape (voir leurs
    /// propres docs) - chacune compare ce solde APRÈS sa propre dépense (déjà incluse dans la somme
    /// ci-dessous) à zéro, jamais un solde partiel isolé.</summary>
    public int EndOfGameTreasuryRemaining =>
        TreasuryWithGains
        - (SelectedEquippedHenchmanGroupOption?.EquipmentCost ?? 0)
        - RareItemPurchaseTotalCost
        - (SelectedNewHiredSword?.HireCost ?? 0)
        - HiredSwordUpkeepEntries.Where(e => e.WillPay == true).Sum(e => e.UpkeepCost)
        - DramatisPersonaUpkeepEntries.Where(e => e.WillPay == true && !e.IsWyrdstoneFee).Sum(e => e.UpkeepCost)
        - PairPaymentAmountCommitted
        - HeroRecruitRows.Sum(r => r.Count * r.Cost)
        - HeroEquipmentTotalCost()
        - HenchmanRecruitmentTotalCost()
        - PurchasedReserveItems.Sum(p => p.Cost)
        + PendingSales.Sum(c => c.SellPrice);

    /// <summary>Alias générique d'IsRareItemPurchaseBlocked (même flag, EndOfGameTreasuryRemaining &lt; 0)
    /// - nom neutre pour être réutilisé par le bandeau "Suivant bloqué" de plusieurs étapes distinctes
    /// (Achat/Vente d'équipement, Détail Héros recruté, Équipement Hommes de main, Vétérans), pas juste
    /// Objets rares (2026-09-23, retour utilisateur - même dialog qui clignote après le picker déjà
    /// réglée une fois pour les limites d'armes, à généraliser ici plutôt que dupliquer une propriété par
    /// étape). IsRareItemPurchaseBlocked reste tel quel (déjà équivalent), pas de renommage superflu.</summary>
    public bool IsTreasuryBlocked => EndOfGameTreasuryRemaining < 0;

    /// <summary>Notifie tout ce qui dépend de EndOfGameTreasuryRemaining - un seul point d'entrée plutôt
    /// qu'une longue liste de [NotifyPropertyChangedFor] dupliquée sur chaque propriété/collection qui
    /// influence ce solde, pour réduire le risque d'en oublier une (voir le bug IsPairDuelStep du
    /// 2026-09-03, causé par exactement ce genre de liste manuelle incomplète). Appelée depuis chaque
    /// point du wizard qui modifie un poste de dépense.</summary>
    private void NotifyTreasuryChanged()
    {
        OnPropertyChanged(nameof(EndOfGameTreasuryRemaining));
        OnPropertyChanged(nameof(RareItemPurchaseRemainingTreasury));
        OnPropertyChanged(nameof(RareItemPurchaseRemainingTreasuryDisplay));
        OnPropertyChanged(nameof(IsRareItemPurchaseBlocked));
        OnPropertyChanged(nameof(IsTreasuryBlocked));
        OnPropertyChanged(nameof(CanAffordNewHiredSword));
        OnPropertyChanged(nameof(CanAffordEquippedHenchman));
        OnPropertyChanged(nameof(EquippedHenchmanTreasuryAfter));
        OnPropertyChanged(nameof(IsPairCorruptionUnaffordable));
        OnPropertyChanged(nameof(IsPairRetentionUnaffordable));
        OnPropertyChanged(nameof(DramatisPersonaeBaselineTreasuryDisplay));
        OnPropertyChanged(nameof(WantsEquipmentSeizure));
        OnPropertyChanged(nameof(WantsDuel));
        OnPropertyChanged(nameof(SeizedEquipmentItems));
        OnPropertyChanged(nameof(SeizedEquipmentTotalValue));
        OnPropertyChanged(nameof(SeizedEquipmentTotalDisplay));
        OnPropertyChanged(nameof(RecruitmentTreasuryDisplay));
        OnPropertyChanged(nameof(PurchasedReserveItemsCost));
        OnPropertyChanged(nameof(PendingSalesTotal));
        OnPropertyChanged(nameof(EquipmentTradingNetTotal));
    }

    /// <summary>ExplorationResult ne s'ajoute que si un résultat a effectivement été déclenché par le
    /// jet précédent (au plus un, voir ExplorationChart.DetectMultiples) - même principe que les étapes
    /// Blessure/Progression qui n'apparaissent que pour les guerriers concernés, plutôt qu'une seule
    /// étape monolithique jet+résolution (retravaillé le 2026-08-17 suite à un retour explicite sur
    /// l'UX).</summary>
    private List<WizardStep> Steps
    {
        get
        {
            var steps = new List<WizardStep> { new(StepKind.Result), new(StepKind.ScenarioRewards), new(StepKind.OutOfAction) };
            // Vendu aux Fosses (2026-09-04, retour utilisateur) : sa propre étape juste après la carte
            // Blessure du guerrier concerné plutôt qu'un bloc embarqué dans la même carte - même position
            // "juste après" que PairDuel après PairEngagement. D'où une boucle plutôt qu'un simple
            // Select : il faut pouvoir insérer une deuxième étape conditionnelle entre deux guerriers.
            // Une étape PAR OCCURRENCE (retour utilisateur, même message) : le jet principal ET chaque
            // sous-jet "Blessures multiples" qui tombe lui-même sur 65 déclenchent chacun leur propre
            // combat de gladiateur (SubRoll distingue l'occurrence, voir WizardStep/CurrentPitFightOutcome)
            // - un guerrier peut donc enchaîner plusieurs étapes Vendu aux Fosses dans la même Fin de
            // Partie.
            foreach (var r in WarriorRows.Where(w => w.IsOutOfAction))
            {
                steps.Add(new WizardStep(StepKind.Injury, r));
                if (r.ShowSoldToThePits) steps.Add(new WizardStep(StepKind.PitFight, r));
                foreach (var sub in r.MultipleInjuryRolls)
                    if (sub.ShowSoldToThePits) steps.Add(new WizardStep(StepKind.PitFight, r, SubRoll: sub));
            }
            // Prisonniers ennemis : une seule étape pour toute la bande (pas par guerrier, contrairement à
            // Blessure/Progression) - ce que CETTE bande a capturé sur l'adversaire, sans rapport avec les
            // guerriers propres mis hors de combat ci-dessus. Ajout demandé par l'utilisateur (2026-08-27),
            // hors périmètre initial du wizard (voir la doc de classe).
            steps.Add(new(StepKind.Captives));
            steps.Add(new(StepKind.Experience));
            steps.AddRange(WarriorRows.Where(r => r.HasMilestone).Select(r => new WizardStep(StepKind.Advance, r)));
            steps.Add(new(StepKind.ExplorationRoll));
            if (TriggeredExplorationResult is not null) steps.Add(new(StepKind.ExplorationResult));
            // Deuxième passage Progression, placé APRÈS Exploration plutôt qu'inséré à la position
            // "normale" (juste après Expérience, ci-dessus) : Steps est recalculée à chaud (voir la doc de
            // classe) - un guerrier dont HasMilestone ne devient vrai qu'une fois l'XP d'Exploration
            // assignée franchirait sinon un palier à une position DÉJÀ dépassée par le joueur, décalant
            // silencieusement tous les StepIndex suivants (StepIndex est un simple entier, sans identité
            // de step stable). Toujours ajouté après ExplorationResult, jamais avant : ce guerrier a donc
            // déjà quitté cette position au moment où le palier apparaît, aucun décalage rétroactif possible.
            steps.AddRange(WarriorRows.Where(r => r.HasExplorationMilestone).Select(r => new WizardStep(StepKind.Advance, r, IsExplorationAdvance: true)));
            // Vente de pierre magique (séquence livre, étape 4) : rien à vendre si le stock est vide, en
            // comptant l'ancien stock ET ce que CETTE partie vient de trouver (voir MaxShardsToSell) -
            // même principe que HiredSwords ci-dessous (étape entièrement absente plutôt que montrée
            // vide). Disponibilité des Vétérans (étape 5) : toujours présente, un jet a lieu à CHAQUE Fin
            // de Partie que la bande ait ou non l'intention d'embaucher (voir Warband.
            // AvailableVeteranExperience) - même statut "toujours là" qu'Expérience/Prisonniers.
            if (MaxShardsToSell > 0) steps.Add(new(StepKind.WyrdstoneSale));
            steps.Add(new(StepKind.AvailableVeterans));
            // Objets rares (étape 6) : absente sans Héros éligible (survivant, pas Hors de combat cette
            // partie - "Warriors taken out of action during the last battle may not look for rare
            // items"), même principe que WyrdstoneSale ci-dessus. Achat : étape SÉPARÉE (retour
            // utilisateur 2026-08-28 - jamais un achat automatique sur un jet réussi), absente si aucune
            // recherche n'a abouti (voir Steps() recalculée à chaud - un jet raté à l'étape précédente ne
            // laisse rien à acheter, cette carte disparaît d'elle-même). IsFound (pas IsSuccess) - bug
            // trouvé 2026-09-01 (retour utilisateur, en testant le paiement de Johann) : IsSuccess ne
            // couvre que le mode Objet, donc l'étape entière était sautée dès qu'un Héros n'avait trouvé
            // qu'un Personnage spécial (IsCharacterFound) et aucun objet - RareItemsWithResults (le
            // contenu affiché À L'INTÉRIEUR de cette étape) utilisait déjà IsFound correctement, seule la
            // condition d'INCLUSION de l'étape elle-même avait été oubliée lors de l'ajout du mode
            // Personnage (2026-08-31).
            if (HasEligibleHeroesForRareItems) steps.Add(new(StepKind.RareItems));
            if (RareItemSearchEntries.Any(e => e.IsFound)) steps.Add(new(StepKind.RareItemPurchase));
            // Dramatis Personae : solde des personnages à frais en or/pierre magique déjà engagés
            // (Johann/Veskit/Marianna/Nicodemus) - voir EndOfGamePageViewModel.DramatisPersonae.cs.
            // Aucun recrutement combiné ici contrairement à Francs-Tireurs (le recrutement d'un Dramatis
            // Persona passe déjà par la recherche "Personnage spécial", RareItems/RareItemPurchase
            // ci-dessus) - entièrement absente si aucun personnage concerné.
            if (HasDramatisPersonaeUpkeep) steps.Add(new(StepKind.DramatisPersonae));
            // Une Poignée d'Or (2026-09-04, retour utilisateur - "il faut la mettre au moment du paiement
            // de recrutement... je me tate sur l'écran de corruption à le faire après dans un autre step
            // pour éviter de mélanger et de surcharger") : sa propre étape juste après Dramatis Personae,
            // regroupe Corruption (ShowPairCorruptionOption - bande qui NE possède PAS la paire) et
            // Rétention/"C'est l'heure de payer !" (ShowPairRetentionOption - bande qui la possède DÉJÀ),
            // mutuellement exclusives par bande - voir EndOfGamePageViewModel.PairEngagement.cs. Vivait
            // avant ça scindée entre l'étape Prisonniers (Corruption) et Dramatis Personae (Rétention).
            if (ShowPairCorruptionOption || ShowPairRetentionOption) steps.Add(new(StepKind.PairEngagement));
            // Duel avec le meneur : sa propre étape juste après (2026-09-03, retour utilisateur - "le duel
            // n'a pas une position fixe, on l'insère là où on en a besoin") - un seul point d'ancrage
            // possible désormais que Corruption/Rétention partagent la même étape juste au-dessus (avant
            // le 2026-09-04, il fallait DEUX points d'ancrage distincts selon le cas).
            if (WantsDuel) steps.Add(new(StepKind.PairDuel));
            // Recrutement (livre, étape 8 - "Hire New Recruits & Buy Common Items" - "this is done in any
            // order and may be done several times") : refondu en étapes séquentielles dédiées (2026-09-21,
            // retour utilisateur - "on manque d'homogénéité... je pense que pour que ce soit plus simple, on
            // va reprendre le système qu'on a dans WarbandEditDialog... écran par écran, step by step") après
            // qu'une première version à 2 onglets librement navigables (Guerriers/Équipement, une seule
            // carte pour Héros ET Hommes de main) se soit révélée confuse à l'usage. Voir
            // EndOfGamePageViewModel.Recruitment.cs pour le détail de chaque étape. Achat/Vente
            // d'équipement (juste avant, inchangée) remplit/vide la réserve de toute la bande ; ce qui suit
            // source ses achats en priorité depuis cette réserve plutôt que d'acheter systématiquement au
            // plein tarif - voir BuildReserve (EndOfGamePageViewModel.Reserve.cs).
            // Renvoyer (livre des règles - "Disbanding a Warband" + FAQ officielle citée par
            // l'utilisateur 2026-09-22 - "you are allowed to dismiss any warrior at any time during the
            // post-battle sequence... transfer the warrior's weapons and gear to your stash and then
            // dismiss him") : toujours présente juste avant Achat/Vente, jamais conditionnelle (le joueur
            // peut ne renvoyer personne) - pour que l'équipement récupéré puisse ensuite être vendu OU
            // réutilisé gratuitement pour équiper une nouvelle recrue (Recrutement, plus loin dans ce même
            // wizard). Voir EndOfGamePageViewModel.Dismissal.cs.
            steps.Add(new(StepKind.DismissWarriors));
            steps.Add(new(StepKind.EquipmentTrading));
            // Héros : effectif (steppers, toujours présente) puis UNE étape PAR héros réellement recruté
            // (dynamique, même principe que Blessure/Progression - un guerrier par étape plutôt qu'une
            // liste) combinant nom ET achat d'équipement pour CE héros précis - "ce qui permet d'assigner
            // directement les items au personnage" (retour utilisateur), plutôt que les nommer tous ensemble
            // dans une étape Noms commune à la fin comme la version précédente.
            steps.Add(new(StepKind.RecruitHeroesCount));
            steps.AddRange(RecruitedHeroRows.SelectMany(r => r.NameSlots)
                .Select(slot => new WizardStep(StepKind.RecruitHeroDetail, RecruitHeroSlot: slot)));
            // Hommes de main : top-up des groupes déjà existants (budget vétérans, équipement automatique)
            // dans sa PROPRE étape - absente s'il n'y a aucun groupe existant à renforcer, même principe que
            // WyrdstoneSale/RareItems ci-dessus. Puis un éventuel nouveau groupe, en 3 étapes distinctes
            // (Effectif toujours présente - "may be done several times" - puis Équipement/Noms, absentes
            // toutes les deux si rien n'a été recruté) reprenant tel quel le contenu qui vivait avant dans
            // l'onglet Guerriers/Équipement de l'ancienne étape Recrutement fusionnée.
            if (HasExistingHenchmanGroups) steps.Add(new(StepKind.RecruitVeteranTopUp));
            steps.Add(new(StepKind.RecruitHenchmenCount));
            if (RecruitedHenchmanRows.Any())
            {
                steps.Add(new(StepKind.RecruitHenchmenEquipment));
                steps.Add(new(StepKind.RecruitHenchmenNames));
            }
            // Francs-Tireurs : solde des Francs-Tireurs déjà engagés + recrutement optionnel d'un nouveau -
            // voir EndOfGamePageViewModel.HiredSwords.cs. Après tout le recrutement (2026-09-21, retour
            // utilisateur - "les francs-tireurs sont après le recrutement", confirmé comme une demande de
            // réordonnancement plutôt qu'un bug : Francs-Tireurs est conceptuellement un autre mécanisme de
            // recrutement, le joueur décide en connaissant sa trésorerie ET son roster finaux, recrutement de
            // guerriers compris) - entièrement absente si aucun Franc-Tireur n'est concerné
            // (HasAnyHiredSwordRelevance).
            if (HasAnyHiredSwordRelevance) steps.Add(new(StepKind.HiredSwords));
            // Réallouer l'équipement (livre, étape 9 - "Swap equipment between models as desired") :
            // toute DERNIÈRE étape avant le Récapitulatif, après Achat/Vente ET tout le recrutement (ordre
            // du livre) - voir EndOfGamePageViewModel.Reallocation.cs. Absente si aucun Héros
            // existant/recrue n'est éligible (rien à réallouer).
            if (HasEligibleReallocationCarriers) steps.Add(new(StepKind.EquipmentReallocation));
            steps.Add(new(StepKind.Recap));
            return steps;
        }
    }

    private WizardStep Current
    {
        get
        {
            var steps = Steps;
            return steps[Math.Clamp(StepIndex, 0, steps.Count - 1)];
        }
    }

    /// <summary>Pilote StepViewConverter (Converters/StepViewConverter.cs, 2026-09-22) - une vue par
    /// étape (Steps/*StepView.xaml), construite à la demande, plutôt que les ~27 blocs IsXxxStep que
    /// EndOfGamePage.xaml construisait/liait tous d'un coup à l'ouverture (cause de la lenteur signalée
    /// par l'utilisateur) - ces ~27 propriétés IsXxxStep ont été retirées avec ce refactor, plus aucun
    /// binding ne les lit.</summary>
    public StepKind CurrentStepKind => Current.Kind;

    /// <summary>Le seul héros affiché à l'étape RecruitHeroDetail courante - une étape par héros recruté,
    /// jamais une liste (même principe que CurrentInjuryWarrior/CurrentAdvanceWarrior).</summary>
    public WarriorNameSlot? CurrentRecruitHeroSlot => Current.RecruitHeroSlot;

    /// <summary>"Héros X/Y" - même idiome que InjuryProgressLabel/AdvanceProgressLabel, index de
    /// CurrentRecruitHeroSlot parmi tous les héros recrutés cette étape.</summary>
    public string RecruitHeroProgressLabel
    {
        get
        {
            var slot = CurrentRecruitHeroSlot;
            if (slot is null) return string.Empty;

            var slots = RecruitedHeroRows.SelectMany(r => r.NameSlots).ToList();
            var index = slots.IndexOf(slot);
            return index < 0 ? string.Empty : string.Format(Loc["EndOfGameRecruitHeroProgressLabel"], index + 1, slots.Count);
        }
    }

    /// <summary>Le seul guerrier affiché à l'étape Blessure courante - une étape par guerrier coché
    /// hors de combat, jamais une liste (voir la doc de classe).</summary>
    public WarriorOutcomeRow? CurrentInjuryWarrior => Current.Warrior;

    /// <summary>Occurrence de Vendu aux Fosses résolue par l'étape courante (2026-09-04, retour
    /// utilisateur) : le jet principal de CurrentInjuryWarrior si Current.SubRoll est null, sinon ce
    /// sous-jet précis - les deux exposent la même forme (IPitFightOutcome : WonPitFight/
    /// SoldToPitsRerollRoll/HasSoldToPitsRerollRoll), ce qui permet au XAML de la carte "checkbox
    /// victoire + relance" de rester identique quelle que soit l'occurrence, sans dupliquer ce bloc.
    /// PitFighterProfile/PitFighterEquipment/les cartes de comparaison restent bindées sur
    /// CurrentInjuryWarrior directement (le Gladiateur et le profil du guerrier sont les mêmes quelle
    /// que soit l'occurrence, inutile de les dupliquer par sous-jet).</summary>
    public IPitFightOutcome? CurrentPitFightOutcome => Current.SubRoll is { } sub ? sub : CurrentInjuryWarrior;

    /// <summary>Le sous-jet "Blessures multiples" résolu par cette étape, typé InjurySubRollEntry plutôt
    /// qu'IPitFightOutcome (contrairement à CurrentPitFightOutcome) uniquement pour exposer Label côté
    /// XAML ("Blessure X/Y", sous-titre de l'étape) - null pour le jet principal.</summary>
    public InjurySubRollEntry? CurrentPitFightSubRoll => Current.SubRoll;

    /// <summary>Pilote l'affichage du sous-titre "Blessure X/Y" (CurrentPitFightSubRoll.Label) quand cette
    /// étape résout un sous-jet plutôt que le jet principal - utile dès qu'un guerrier enchaîne plusieurs
    /// combats de gladiateur dans la même Fin de Partie.</summary>
    public bool IsPitFightMainOccurrence => Current.SubRoll is null;

    public string InjuryProgressLabel
    {
        get
        {
            var warrior = CurrentInjuryWarrior;
            if (warrior is null) return string.Empty;

            var outOfAction = WarriorRows.Where(r => r.IsOutOfAction).ToList();
            var index = outOfAction.IndexOf(warrior);
            return index < 0 ? string.Empty : string.Format(Loc["EndOfGameInjuryProgressLabel"], index + 1, outOfAction.Count);
        }
    }

    /// <summary>Le seul guerrier affiché à l'étape Progression courante - une étape par guerrier ayant
    /// franchi un palier d'XP, même principe que CurrentInjuryWarrior (voir la doc de classe).</summary>
    public WarriorOutcomeRow? CurrentAdvanceWarrior => Current.Warrior;

    /// <summary>La bonne collection de jets à afficher/valider pour l'étape Progression courante -
    /// AdvanceRolls (XP de bataille) ou ExplorationAdvanceRolls (XP d'Exploration), selon
    /// WizardStep.IsExplorationAdvance (voir sa doc). Tout le reste de l'étape (AutoRollAdvance,
    /// PickAdvanceSkill...) reste inchangé, cette propriété est le seul point de bascule.</summary>
    public ObservableCollection<AdvanceRollEntry>? CurrentAdvanceRolls =>
        CurrentAdvanceWarrior is { } warrior ? (Current.IsExplorationAdvance ? warrior.ExplorationAdvanceRolls : warrior.AdvanceRolls) : null;

    public string AdvanceProgressLabel
    {
        get
        {
            var warrior = CurrentAdvanceWarrior;
            if (warrior is null) return string.Empty;

            var withMilestone = (Current.IsExplorationAdvance
                ? WarriorRows.Where(r => r.HasExplorationMilestone)
                : WarriorRows.Where(r => r.HasMilestone)).ToList();
            var index = withMilestone.IndexOf(warrior);
            return index < 0 ? string.Empty : string.Format(Loc["EndOfGameAdvanceProgressLabel"], index + 1, withMilestone.Count);
        }
    }

    public bool CanGoBack => StepIndex > 0;
    public bool IsLastStep => StepIndex >= Steps.Count - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], StepIndex + 1, Steps.Count);

    /// <summary>Coché "Sacrifié" seulement pour le Culte des Possédés (bande précise, pas la race
    /// "Humain du Chaos" qu'elle partage avec la Kermesse du Chaos) et "Tué (Zombie)" seulement pour les
    /// Morts-Vivants - match par nom anglais de l'archétype de bande jouée (_warbandArchetypeName), même
    /// idiome que ExplorationOutcome.RestrictedToWarbandArchetypeNames.</summary>
    private bool IsUndeadWarband => _warbandArchetypeName == "Undead";
    private bool IsPossessedWarband => _warbandArchetypeName == "Cult of the Possessed";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryCapturedEnemiesText))]
    private bool hasCapturedEnemies;

    partial void OnHasCapturedEnemiesChanged(bool value) => SyncCapturedEnemies();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryCapturedEnemiesText))]
    private int capturedEnemyCount = 1;

    partial void OnCapturedEnemyCountChanged(int value) => SyncCapturedEnemies();

    public ObservableCollection<CapturedEnemyEntry> CapturedEnemies { get; } = new();

    /// <summary>Ajoute/retire des entrées en bout de liste pour matcher CapturedEnemyCount, sans jamais
    /// recréer celles déjà là - même principe que WarriorOutcomeRow.SyncFigureInjuryRolls (préserve la
    /// saisie déjà faite sur les prisonniers 1..N si le joueur ajuste le compte après coup). Vide
    /// entièrement si HasCapturedEnemies repasse à faux.</summary>
    private void SyncCapturedEnemies()
    {
        if (!HasCapturedEnemies)
        {
            CapturedEnemies.Clear();
            return;
        }

        while (CapturedEnemies.Count < CapturedEnemyCount)
            CapturedEnemies.Add(new CapturedEnemyEntry(CapturedEnemies.Count + 1, IsUndeadWarband, IsPossessedWarband, Loc));
        while (CapturedEnemies.Count > CapturedEnemyCount)
            CapturedEnemies.RemoveAt(CapturedEnemies.Count - 1);
    }

    public string SummaryCapturedEnemiesText => HasCapturedEnemies
        ? string.Format(Loc["EndOfGameCapturedEnemiesSummary"], CapturedEnemyCount)
        : string.Empty;

    /// <summary>Bande éditée par ce wizard - résolue depuis WarbandId (QueryProperty) dans
    /// InitializeAsync, jamais null une fois cette dernière terminée avec succès (voir son garde).
    /// Membre plutôt que variable locale : le pipeline Apply*Async (EndOfGamePageViewModel.Apply.cs) en
    /// a besoin comme WarbandDetailViewModel.Warband en avait besoin avant ce refactor.</summary>
    public Warband? Warband { get; private set; }

    /// <summary>Every living, active-roster warrior across all four groups (Heroes/Henchmen/HiredSwords/
    /// DramatisPersonae), Sick included, Dead/Retired excluded - même ensemble que
    /// WarbandDetailViewModel.AllActiveWarriorRows (voir sa doc), nécessaire ici uniquement pour
    /// retrouver "le chef" pour quelques bonus étroits d'Exploration/Prisonniers (voir
    /// ApplyExplorationOutcomeAsync/ApplyCapturedEnemiesAsync), qu'il participe ou non à CETTE bataille.
    /// Instantané figé pris une fois dans InitializeAsync, même raisonnement que tous les autres
    /// instantanés pré-bataille de ce ViewModel (_originalReserveSnapshot etc.) - jamais rechargé en cours de
    /// wizard.</summary>
    private List<WarriorRow> _allActiveWarriorRows = new();

    /// <summary>Sous-ensemble Hommes de main de _allActiveWarriorRows - même filtre que
    /// WarbandDetailViewModel.Henchmen (voir sa doc), nécessaire au pipeline Apply*Async (ex. fusionner
    /// une trouvaille de Traînard/Prisonniers/"Tué et transformé en Zombie" dans un groupe déjà existant
    /// plutôt que d'en créer un nouveau) - même instantané figé pré-bataille que Henchmen l'était côté
    /// WarbandDetailViewModel avant ce refactor (chargé une fois, jamais republié en cours de wizard).</summary>
    private IEnumerable<WarriorRow> Henchmen => _allActiveWarriorRows.Where(r => !r.Warrior.IsHero && !r.Warrior.IsHiredSword && !r.Warrior.IsDramatisPersona);

    partial void OnWarbandIdChanged(int value) => _ = InitializeAsync(value);

    public EndOfGamePageViewModel(ISkillPickerService skillPicker, IDetailDialogService detailDialogs, ILibraryService libraryService,
        IHiredSwordPickerService hiredSwordPicker, IEquipmentPickerService equipmentPicker, ISellEquipmentPickerService sellEquipmentPicker,
        IDramatisPersonaPickerService dramatisPersonaPicker, IWarbandService warbandService, IMutationPickerService mutationPicker)
    {
        _skillPicker = skillPicker;
        _detailDialogs = detailDialogs;
        _libraryService = libraryService;
        _hiredSwordPicker = hiredSwordPicker;
        _equipmentPicker = equipmentPicker;
        _sellEquipmentPicker = sellEquipmentPicker;
        _dramatisPersonaPicker = dramatisPersonaPicker;
        _warbandService = warbandService;
        _mutationPicker = mutationPicker;

        // Déplacé depuis InitializeAsync (2026-09-22, retour utilisateur - "on a perdu la valeur par
        // défaut du victoire défaite") : contrairement au reste de InitializeAsync, ces 3 options sont
        // fixes (jamais dépendantes de WarbandId/du chargement DB) - les construire ici, dans le
        // constructeur synchrone, garantit qu'elles existent AVANT que la page ne pose son premier
        // binding (ResultStepView est la toute première vue construite, StepKind.Result étant
        // inconditionnellement l'étape 0 - voir Steps). Les poser dans InitializeAsync (async, après
        // le binding déjà posé) laissait une fenêtre où le Picker se liait sur une liste encore vide,
        // avant qu'un défaut ne soit sélectionné - la même classe de bug que WarriorRows/
        // RareItemSearchEntries plus tôt dans cette session, réglée cette fois en évitant carrément la
        // course plutôt qu'en notifiant après coup.
        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        SelectedResult = ResultOptions[0];
    }

    /// <summary>Reconstruit tout l'état du wizard depuis WarbandId seul (Shell [QueryProperty], voir
    /// OnWarbandIdChanged) - remplace l'ancien constructeur de EndOfGameDialogViewModel (~25 paramètres)
    /// ET la préparation qui vivait avant dans WarbandDetailViewModel.EndOfGame() (catalogues,
    /// dictionnaires anglais->localisé, snapshot d'inventaire) : tout est une fonction pure de
    /// (warbandId, état DB actuel, langue courante), rien n'est encore aléatoire/dépendant d'une
    /// interaction utilisateur à ce stade - re-dériver depuis WarbandId seul est donc correct, pas un
    /// compromis. Le garde "Warband null"/"aucun guerrier actif" reste côté WarbandDetailViewModel.
    /// EndOfGame() (avant la navigation), pour ne jamais pousser cette page sur un cas où il n'y a rien à
    /// enregistrer.</summary>
    private async Task InitializeAsync(int warbandId)
    {
        await Loading.RunAsync(async () =>
        {
            Warband = await _warbandService.GetWarbandAsync(warbandId);
            if (Warband is null) return;

            var language = LocalizationService.Instance.Language;
            var explorationResults = await _libraryService.GetExplorationResultsAsync(language);

            // ExplorationOutcome.EquipmentItemName référence le catalogue par nom ANGLAIS brut (voir sa
            // doc) plutôt que par Id - construit une seule fois ici (Id introuvable autrement en anglais
            // uniquement) et transmis au wizard sous forme d'EquipmentItem entier (pas juste son nom)
            // pour qu'il affiche un vrai ChipView tapable (icône + popup détail) résolu dans la langue
            // courante, au lieu du nom anglais tel quel (ex. "Axe" affiché même en français avant ce
            // correctif).
            var englishEquipment = await _libraryService.GetEquipmentItemsAsync("en");
            var localizedEquipment = language == "en" ? englishEquipment : await _libraryService.GetEquipmentItemsAsync(language);
            var equipmentItemsByEnglishName = englishEquipment.ToDictionary(e => e.Name,
                e => localizedEquipment.FirstOrDefault(l => l.Id == e.Id) ?? e);

            // Même besoin pour ExplorationOutcome.MaterialRuleName (ex. "Ornate Weapon") - permet au
            // wizard d'afficher "Épée (O)" plutôt que le nom nu, comme n'importe quel objet en
            // Gromril/Ithilmar.
            var englishSpecialRules = await _libraryService.GetSpecialRulesAsync("en");
            var localizedSpecialRules = language == "en" ? englishSpecialRules : await _libraryService.GetSpecialRulesAsync(language);
            var specialRulesByEnglishName = englishSpecialRules.ToDictionary(r => r.Name,
                r => localizedSpecialRules.FirstOrDefault(l => l.Id == r.Id) ?? r);

            // Pour EquipmentItem.GrantsSpecificSkillName (ex. Haggle du symbole de la Maison du Marchand)
            // - seul l'Id compte pour le picker de compétence (son propre catalogue est déjà localisé),
            // pas besoin de résoudre un objet Skill localisé comme les deux dictionnaires ci-dessus.
            var skillIdsByEnglishName = (await _libraryService.GetSkillsAsync("en")).ToDictionary(s => s.Name, s => s.Id);

            // ExplorationOutcome.RestrictedToWarbandArchetypeNames (Groupe B "conditionné par la bande" -
            // Traînard, Prisonniers, Cimetière, bénédiction du Sanctuaire) matche par nom anglais, pas Id
            // - même besoin que les dictionnaires ci-dessus.
            var warbandArchetypeName = (await _libraryService.GetWarbandArchetypesAsync("en"))
                .First(a => a.Id == Warband.WarbandArchetypeId).Name;

            // Étape "Recrutement" (livre, étape 8) - MaxWarriors pour RecruitmentRules.CanRecruit, voir
            // EndOfGamePageViewModel.Recruitment.cs. Null impossible en pratique (l'archétype de CETTE
            // bande, résolu depuis Warband.WarbandArchetypeId).
            var warbandArchetype = (await _libraryService.GetWarbandArchetypeAsync(Warband.WarbandArchetypeId, language))!;
            var bandWideSpecialRules = warbandArchetype.SpecialRules ?? new List<SpecialRule>();
            var bandMagicSchools = warbandArchetype.MagicSchools ?? new List<MagicSchool>();

            // Toutes les bandes (pas seulement la nôtre) - une règle de Haine peut cibler n'importe
            // laquelle, voir WarriorRosterBuilder.BuildSpecialRuleChips.
            var allWarbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(language);
            var warbandArchetypeNames = allWarbandArchetypes.ToDictionary(a => a.Id, a => a.Name);

            // Pour ExplorationOutcome.GrantsFreeHenchmanArchetypeName (ex. "Zombie", Traînard) - limité
            // aux archétypes de CETTE bande (jamais besoin d'un autre archétype pour ce genre de branche).
            var englishWarriorArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, "en");
            var localizedWarriorArchetypes = language == "en" ? englishWarriorArchetypes : await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, language);
            var warriorArchetypesByEnglishName = englishWarriorArchetypes.ToDictionary(a => a.Name,
                a => localizedWarriorArchetypes.FirstOrDefault(l => l.Id == a.Id) ?? a);

            // Pour l'aperçu en direct des SpecialRules attachées à une branche de Blessure Grave (ex.
            // Folie 24 -> Stupidité/Frénésie) - déjà pleinement résolu (SpecialRules incluses, voir
            // LibraryService.GetInjuriesAsync) donc réutilisable tel quel par WarriorOutcomeRow, via
            // InjuryCatalogLookup (même logique de correspondance par jet que GetOrCreateInjuryAsync,
            // partagée pour ne pas dupliquer le parseur de RollRange).
            var injuryCatalog = await _libraryService.GetInjuriesAsync(language);

            // Pour la comparaison de profil de "Vendu aux Fosses" (65) - le Gladiateur ("Pit Fighter",
            // catalogue Franc-Tireur/HiredSword) est un adversaire éphémère (jamais recruté dans la
            // bande, voir Models.Library.HiredSword), simple lookup par nom anglais comme les
            // dictionnaires ci-dessus.
            var englishHiredSwords = await _libraryService.GetHiredSwordsAsync("en");
            // Catalogue complet localisé - à la fois pour le Gladiateur éphémère ci-dessous (Vendu aux
            // Fosses) ET, sans rapport avec lui, pour l'étape "Francs-Tireurs" (upkeep/recrutement réel
            // dans NOTRE bande - voir EndOfGamePageViewModel.HiredSwords.cs).
            var localizedHiredSwords = language == "en" ? englishHiredSwords : await _libraryService.GetHiredSwordsAsync(language);
            var pitFighterEnglish = englishHiredSwords.FirstOrDefault(h => h.Name == "Pit Fighter");
            var pitFighterProfile = pitFighterEnglish is null
                ? null
                : localizedHiredSwords.FirstOrDefault(h => h.Id == pitFighterEnglish.Id) ?? pitFighterEnglish;

            // Équipement de départ du Gladiateur, déjà résolu en vrais EquipmentItem (localisés) pour la
            // même carte comparative - même idiome que localizedEquipment ci-dessus.
            var pitFighterEquipment = pitFighterProfile is null
                ? new List<EquipmentItem>()
                : localizedEquipment.Where(e => pitFighterProfile.StartingEquipmentIds.Contains(e.Id)).ToList();

            // Étape "Dramatis Personae" (upkeep, voir EndOfGamePageViewModel.DramatisPersonae.cs) + choix
            // "payer avec l'objet alternatif" de l'étape Achat (RareItemSearchEntry.
            // HasAlternativePaymentOption) - même catalogue localisé que localizedHiredSwords ci-dessus.
            var dramatisPersonaCatalog = await _libraryService.GetDramatisPersonaeAsync(language);
            // Équipement de départ de chaque Dramatis Persona (Marquand & Ulli notamment) - même helper
            // que DetailDialogService.ShowDramatisPersonaDetailDialogAsync (EquipmentQuantityChip.
            // GroupFrom, gère les doublons).
            var dramatisPersonaStartingEquipmentById = dramatisPersonaCatalog.ToDictionary(p => p.Id,
                p => p.StartingEquipmentIds.Count == 0 ? new List<EquipmentQuantityChip>() : EquipmentQuantityChip.GroupFrom(p.StartingEquipmentIds, localizedEquipment));

            var warbandInventory = await _warbandService.GetWarbandEquipmentAsync(warbandId, language);
            var ownedEquipmentItemIds = warbandInventory.Select(w => w.Item.Id).ToHashSet();

            // Délai de re-recherche (2026-09-01, Aenur/Ulli & Marquand - voir DramatisPersona.
            // RequiresCooldownBeforeResearch) - exclut du picker "Personnage spécial" tout personnage
            // encore en cooldown pour CETTE bande.
            var cooldownDramatisPersonaIds = await _warbandService.GetDramatisPersonaCooldownIdsAsync(Warband.Id);

            var rosterBuilder = new WarriorRosterBuilder(localizedWarriorArchetypes, localizedHiredSwords, dramatisPersonaCatalog,
                bandWideSpecialRules, bandMagicSchools, warbandArchetypeNames);
            var allWarriors = await _warbandService.GetWarriorsAsync(warbandId, language);
            var allRows = rosterBuilder.BuildRows(allWarriors);
            // Même filtre que WarbandDetailViewModel.AllActiveWarriorRows (voir sa doc) : exclut
            // Mort/Retraité, garde Malade (voir _allActiveWarriorRows's own doc). Ordre forcé Chef
            // d'abord, puis autres Héros (Francs-Tireurs/Dramatis Personae inclus, IsHero vaut vrai pour
            // eux aussi), puis groupes d'Hommes de main - retour utilisateur 2026-09-22, pour que toutes
            // les listes de ce wizard qui énumèrent WarriorRows dans cet ordre (Hors de combat, Blessure,
            // Expérience, Progression...) le respectent. OrderBy est un tri stable : au sein d'un même
            // groupe, l'ordre relatif renvoyé par la base (allWarriors) est conservé.
            _allActiveWarriorRows = allRows.Where(r => !r.IsDead && !r.IsRetired)
                .OrderBy(r => r.Warrior.IsLeader ? 0 : r.Warrior.IsHero ? 1 : 2)
                .ToList();
            var activeWarriorRows = _allActiveWarriorRows.Where(r => r.Warrior.Status == WarriorStatus.Active).ToList();

            _warbandArchetypeId = Warband.WarbandArchetypeId;
            _warbandArchetypeName = warbandArchetypeName;
            _pendingExplorationBonusDie = Warband.PendingExplorationBonusDie;
            _hasCatacombReroll = Warband.HasCatacombReroll;
            _currentTreasury = Warband.Treasury;
            _currentWyrdstoneShards = Warband.WyrdstoneShards;
            _explorationResults = explorationResults;
            _equipmentItemsByEnglishName = equipmentItemsByEnglishName;
            _warriorArchetypesByEnglishName = warriorArchetypesByEnglishName;
            _specialRulesByEnglishName = specialRulesByEnglishName;
            // Une ligne PreExisting par ligne WarbandEquipment réelle, jamais fusionnées entre elles
            // (chacune garde son SourceId réel) - BuildReserve repart de cet instantané à chaque lecture.
            foreach (var stashItem in warbandInventory)
                _originalReserveSnapshot.Add(new ReserveLine(stashItem.Item, stashItem.MaterialRule, stashItem.Quantity, ReserveLineOrigin.PreExisting, stashItem.Id, stashItem.FoundValueOverride));
            _skillIdsByEnglishName = skillIdsByEnglishName;
            _hiredSwordCatalog = localizedHiredSwords;
            _dramatisPersonaCatalog = dramatisPersonaCatalog;
            _dramatisPersonaStartingEquipmentById = dramatisPersonaStartingEquipmentById;
            _ownedEquipmentItemIds = ownedEquipmentItemIds;
            _cooldownDramatisPersonaIds = cooldownDramatisPersonaIds;
            _recruitableWarbandArchetype = warbandArchetype;
            _englishEquipmentCatalog = englishEquipment;
            _englishSpecialRulesCatalog = englishSpecialRules;
            _localizedEquipmentCatalog = localizedEquipment;
            foreach (var archetype in localizedWarriorArchetypes)
                RecruitRows.Add(new WarriorRecruitRow(archetype, isEditingWarband: false));

            // Snapshot pour AdvanceRollEntry.CanPromote (promotion Homme de main -> Héros, jet 10-12) -
            // voir sa doc pour les limites acceptées (ne suit pas les promotions résolues plus tôt dans
            // la même Fin de Partie ; se base sur activeWarriorRows - un Héros Malade cette partie, donc
            // absent d'activeWarriorRows, n'est pas compté ici, sous-estimation mineure du plafond de 6
            // acceptée plutôt que de faire remonter le roster complet de la bande jusqu'à ce ViewModel).
            var startingHeroCount = activeWarriorRows.Count(r => r.Warrior.IsHero);
            WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriorRows.Select(r =>
                new WarriorOutcomeRow(r.Warrior, r.RoleName, r.Warrior.GainsExperience, r.MagicSchools, startingHeroCount, injuryCatalog, pitFighterProfile, pitFighterEquipment, r.SpecialRules)));

            // Effectif déjà recruté de chaque type, poussé sur CountDisplay (voir WarriorRecruitRow.
            // ExternalHeadCount) - doit suivre WarriorRows (ExistingCountForArchetype s'appuie dessus), pas
            // avant.
            foreach (var row in RecruitRows)
                row.ExternalHeadCount = ExistingCountForArchetype(row.Archetype.Id);

            BuildHiredSwordUpkeepEntries();
            BuildDramatisPersonaUpkeepEntries();
            BuildPairCorruptionOption();
            BuildPairRetentionOption();
            BuildExistingHenchmanTopUps();

            WireUpChangeNotifications();

            // Tout ce qui précède mute un grand nombre de propriétés (calculées ou { get; private set; })
            // qui n'ont pas leur propre [ObservableProperty] - dans l'ancienne dialog, tout ce chargement
            // tournait dans le constructeur, AVANT que la dialog ne soit même construite/liée en XAML,
            // donc aucune notification n'était nécessaire nulle part. Une page Shell lie son
            // BindingContext dès sa construction, avant que cet InitializeAsync (async) n'ait tourné - les
            // bindings déjà posés (Une Poignée d'Or/ShowPairRetentionOption, Francs-Tireurs, Renvoyer,
            // Recrutement...) restent donc accrochés à l'état initial vide tant que rien ne les notifie
            // explicitement (bug trouvé le 2026-09-22, retour utilisateur - plusieurs étapes sans aucune
            // donnée visible). Plutôt que de traquer un par un chaque propriété concernée (surface bien
            // trop large sur un ViewModel de cette taille), un seul PropertyChanged avec un nom vide -
            // idiome standard "toutes les propriétés ont changé", respecté par le moteur de binding MAUI -
            // force TOUS les bindings déjà posés sur cette page à se relire une fois le chargement fini.
            OnPropertyChanged(string.Empty);
        });
    }

    /// <summary>Tous les abonnements PropertyChanged posés jusque-là dans le constructeur (avant que
    /// InitializeAsync le remplace) - déplacés tels quels dans leur propre méthode plutôt que dans le
    /// bloc Loading.RunAsync ci-dessus, purement pour la lisibilité (le bloc était déjà long avant
    /// l'ajout de tout le chargement de catalogues qui vivait avant dans WarbandDetailViewModel.
    /// EndOfGame()).</summary>
    private void WireUpChangeNotifications()
    {
        // Payer/Renvoyer un Johann/Veskit/Marianna/Nicodemus change EndOfGameTreasuryRemaining (une
        // solde en or payée ici dispute la même trésorerie que tout le reste du wizard, 2026-09-03) -
        // sans cette notification, le bloc "Où est l'Argent ?" (et tout le reste : Achat d'Objets rares,
        // Francs-Tireurs, Homme de main équipé) resterait périmé si le joueur coche "Payer" sur une
        // solde APRÈS avoir déjà pris une autre décision financière ailleurs dans le wizard.
        foreach (var entry in DramatisPersonaUpkeepEntries)
        {
            entry.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(DramatisPersonaUpkeepEntry.SelectedChoiceLabel)) return;
                NotifyTreasuryChanged();
            };
        }

        // Engager/Payer/Renvoyer un Franc-Tireur change EndOfGameTreasuryRemaining, même raison que la
        // subscription DramatisPersonaUpkeepEntries ci-dessus - jusqu'ici manquante (2026-09-03) : aucune
        // notification n'existait pour SelectedChoiceLabel des soldes déjà engagées.
        foreach (var entry in HiredSwordUpkeepEntries)
        {
            entry.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(HiredSwordUpkeepEntry.SelectedChoiceLabel)) return;
                NotifyTreasuryChanged();
            };
        }

        // Une entrée par Héros (jamais un Homme de main - "Whenever a Hero wants to buy a rare item") -
        // construites une fois ici comme WarriorRows, celles des Héros mis Hors de combat restent dans la
        // collection mais masquées côté XAML (IsOutOfAction, voir HasEligibleHeroesForRareItems) plutôt
        // que retirées, même principe que ShowsInExperienceStep pour l'étape Expérience. Dramatis
        // Personae exclus (2026-09-03, retour utilisateur - "les dramatis personae ne peuvent pas
        // trouver des objets rares/personnages spéciaux, c'est que les héros") : IsHero vaut TRUE pour
        // eux (même flux Blessures Graves/XP qu'un vrai Héros), mais ce sont des figurines à part, pas
        // des Héros au sens de cette règle du livre - même limite déjà corrigée pour SurvivingHeroCount
        // (EndOfGamePageViewModel.Exploration.cs) et connue pour le décompte de tête à la vente de
        // pierre magique (voir DRAMATIS_PERSONAE_STATUS.md).
        RareItemSearchEntries = new ObservableCollection<RareItemSearchEntry>(
            WarriorRows.Where(r => r.Warrior.IsHero && !r.Warrior.IsDramatisPersona).Select(r => new RareItemSearchEntry(r, Loc, _ownedEquipmentItemIds)));

        // L'étape Achat séparée (RareItemPurchase) et son total en direct (RareItemPurchaseRemainingTreasury)
        // dépendent de IsSuccess/IsCharacterFound/WantsToBuy/PriceRoll de chaque entrée - notifie
        // StepLabel/IsLastStep (l'étape peut apparaître/disparaître) et le total à chaque changement,
        // même idiome que la subscription WarriorRows ci-dessus.
        foreach (var entry in RareItemSearchEntries)
        {
            entry.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(RareItemSearchEntry.IsSuccess) or nameof(RareItemSearchEntry.IsCharacterFound))
                {
                    OnPropertyChanged(nameof(StepLabel));
                    OnPropertyChanged(nameof(IsLastStep));
                    // Bug trouvé par test utilisateur : RareItemsWithResults n'est jamais notifiée sans
                    // cette ligne, donc le BindableLayout de l'étape Achat (bound dessus) ne se rafraîchit
                    // jamais après le chargement initial - la carte de l'étape reste vide même quand une
                    // recherche vient de réussir. Même bug possible côté Personnage depuis que
                    // RareItemsWithResults couvre IsCharacterFound aussi (voir RareItemSearchEntry.IsFound).
                    OnPropertyChanged(nameof(RareItemsWithResults));
                }
                // WantsToRecruit/IsCharacterFound/WantsToPayWithAlternativeItem ajoutés 2026-09-01 - un
                // personnage à frais en or (RareItemSearchEntry.HasHireCost) participe maintenant à ce
                // même total (EffectiveHireCostForTreasury), même bug potentiel que RareItemsWithResults
                // ci-dessus s'il manquait ici : la trésorerie restante affichée resterait périmée dès que
                // le joueur coche/décoche "Recruter" ou bascule le paiement alternatif.
                if (e.PropertyName is nameof(RareItemSearchEntry.WantsToBuy) or nameof(RareItemSearchEntry.PriceRoll) or nameof(RareItemSearchEntry.IsSuccess)
                    or nameof(RareItemSearchEntry.WantsToRecruit) or nameof(RareItemSearchEntry.IsCharacterFound) or nameof(RareItemSearchEntry.WantsToPayWithAlternativeItem))
                {
                    OnPropertyChanged(nameof(RareItemPurchaseTotalCost));
                    // Un seul solde de trésorerie pour tout le wizard (2026-09-03, retour utilisateur - voir
                    // EndOfGameTreasuryRemaining/NotifyTreasuryChanged's own doc) : cocher/décocher un achat
                    // ou un recrutement à frais en or ici doit se répercuter partout ailleurs (Francs-Tireurs,
                    // Corruption/Rétention, Homme de main équipé), pas seulement sur cette étape.
                    NotifyTreasuryChanged();
                    // Nicodemus (FeeKind.Wyrdstone) - devise séparée, ne dispute jamais EndOfGameTreasuryRemaining
                    // (2026-09-01, "on a qu'a brancher la wyrstone à son paiement").
                    OnPropertyChanged(nameof(HasWyrdstoneCostEntries));
                    OnPropertyChanged(nameof(RareItemPurchaseTotalWyrdstoneCost));
                    OnPropertyChanged(nameof(RareItemPurchaseRemainingWyrdstoneShards));
                    OnPropertyChanged(nameof(RareItemPurchaseRemainingWyrdstoneShardsDisplay));
                    OnPropertyChanged(nameof(IsRareItemPurchaseWyrdstoneBlocked));
                    // Retour utilisateur (2026-09-02) : "si on a engager la paire, on ne doit pas voir la
                    // case de corruption" - l'étape "Une Poignée d'Or" (case Corruption) suit l'étape
                    // Achat/Recrutement dans l'ordre du wizard (Steps), donc cocher "Recruter" sur un
                    // Marquand/Ulli trouvé ici doit se répercuter dessus - reste notifiée en direct (voir
                    // ShowPairCorruptionOption, calculée plutôt que figée) au cas où le joueur reviendrait
                    // en arrière après l'avoir déjà vue.
                    OnPropertyChanged(nameof(ShowPairCorruptionOption));
                }
                // Basculer Objet/Personnage change quels résultats existent (voir IsFound) - StepLabel
                // seul suffit ici, RareItemsWithResults est déjà notifiée par la branche IsSuccess/
                // IsCharacterFound ci-dessus dès que le nouveau mode produit un résultat.
                if (e.PropertyName is nameof(RareItemSearchEntry.IsSearchingForCharacter))
                {
                    OnPropertyChanged(nameof(StepLabel));
                }
            };
        }

        // Le nombre d'étapes dépend de IsOutOfAction (étapes Blessure) et de HasMilestone (étapes
        // Progression) - Steps recalcule ça à chaque accès, mais on rafraîchit quand même StepLabel/
        // IsLastStep tout de suite pour que le joueur voie le compte à jour pendant qu'il coche des
        // guerriers ou saisit des PX, plutôt que d'attendre le prochain Next/Back.
        foreach (var row in WarriorRows)
        {
            row.PropertyChanged += (_, e) =>
            {
                // DistributedExplorationExperience/LeaderExplorationExperience : Steps() gagne aussi son
                // deuxième passage Progression (HasExplorationMilestone) dès que l'XP d'Exploration
                // change, pas seulement IsOutOfAction/ExperienceGained.
                if (e.PropertyName is nameof(WarriorOutcomeRow.IsOutOfAction) or nameof(WarriorOutcomeRow.ExperienceGained)
                    or nameof(WarriorOutcomeRow.DistributedExplorationExperience) or nameof(WarriorOutcomeRow.LeaderExplorationExperience))
                {
                    OnPropertyChanged(nameof(StepLabel));
                    OnPropertyChanged(nameof(IsLastStep));
                }

                // Répartition d'Expérience entre Héros (Prisonniers, Possédés) : DistributedExperienceRemaining
                // s'appuie sur la somme des DistributedExplorationExperience de chaque Héros, un objet
                // différent du ViewModel lui-même - remonte le changement manuellement, même principe que
                // ExplorationDieEntry ailleurs dans ce fichier.
                if (e.PropertyName == nameof(WarriorOutcomeRow.DistributedExplorationExperience))
                    OnPropertyChanged(nameof(DistributedExperienceRemaining));

                // Renvoyer (case à cocher d'un Héros en binding direct TwoWay, jamais une commande) - seul
                // cet abonnement couvre uniformément la case Héros ET le stepper de groupe (Increment/
                // DecrementDismiss, qui rafraîchissent déjà l'éligibilité de Recrutement elles-mêmes) pour
                // rafraîchir le récap "Équipement récupéré" et l'éligibilité de Recrutement dans le cas
                // Héros.
                if (e.PropertyName == nameof(WarriorOutcomeRow.DismissCount))
                {
                    OnPropertyChanged(nameof(RecoveredDismissedEquipmentChips));
                    UpdateRecruitRowsEligibility();
                    RefreshHenchmanTopUpBreakdowns();
                }
            };
        }
    }

    [RelayCommand]
    private async Task Next()
    {
        if (!ValidateCurrentStep()) return;
        await ShowWeaponLimitWarningIfNeededAsync();
        if (StepIndex < Steps.Count - 1) await ChangeStepAsync(StepIndex + 1);
        if (Current.Kind == StepKind.EquipmentReallocation) CaptureReallocationSnapshot();
    }

    /// <summary>Spinner pendant le changement d'étape (2026-09-24, retour utilisateur) : la vue de
    /// l'étape suivante est construite de façon synchrone sur le thread UI au changement de StepIndex
    /// (StepViewConverter, première visite surtout), ce qui figeait l'écran sans aucun retour visuel.
    /// Le premier délai laisse une frame au spinner pour se peindre AVANT ce travail synchrone ; le
    /// second laisse la nouvelle vue se mesurer/s'afficher avant de le masquer.</summary>
    private async Task ChangeStepAsync(int newIndex)
    {
        Loading.Show();
        try
        {
            await Task.Delay(16);
            StepIndex = newIndex;
            await Task.Delay(16);
        }
        finally
        {
            Loading.Hide();
        }
    }

    /// <summary>Avertissement non-bloquant (2 armes de corps à corps / 2 armes de tir différentes max par
    /// guerrier, livre des règles - voir WeaponLimits) - déclenché au clic sur Suivant plutôt
    /// qu'immédiatement après la fermeture du picker d'équipement (retour utilisateur 2026-09-21 - "on a
    /// plus rien qui s'affiche lorsqu'on valide quelque chose") : un dialog ouvert juste après le picker
    /// (une page plein écran poussée modalement, EquipmentItemSelectorPage) se prenait dans la même course
    /// de navigation modale que la page en train de se dépiler. Au clic sur Suivant, aucune transition
    /// modale n'est plus en cours - le dialog s'affiche normalement. Ne bloque jamais Next() (toujours
    /// appelé APRÈS ValidateCurrentStep, qui seul peut empêcher d'avancer) - juste informatif, comme
    /// c'était déjà le cas avant.</summary>
    private async Task ShowWeaponLimitWarningIfNeededAsync()
    {
        switch (Current.Kind)
        {
            case StepKind.RecruitHeroDetail when CurrentRecruitHeroSlot is { } slot:
                await WarnIfExceedsWeaponLimits(slot.Equipment, slot.Name.Length > 0 ? slot.Name : slot.ArchetypeLabel);
                break;
            case StepKind.RecruitHenchmenEquipment:
                // Plusieurs groupes possibles sur cette étape (contrairement à RecruitHeroDetail, une
                // étape par héros) - un avertissement par groupe fautif, l'un après l'autre.
                foreach (var group in RecruitedHenchmanRows.SelectMany(r => r.HenchmanGroupDrafts))
                    await WarnIfExceedsWeaponLimits(group.Equipment, group.Name);
                break;
        }
    }

    private async Task WarnIfExceedsWeaponLimits(IEnumerable<EquipmentPick> equipment, string targetLabel)
    {
        if (!WeaponLimits.ExceedsLimits(equipment.Select(p => p.Item))) return;
        await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], targetLabel));
    }

    /// <summary>Steppeur de l'étape Expérience (2026-09-25, retour utilisateur - remplace la saisie clavier).
    /// ExperienceGainedText reste la source (vide = 0), seule la façon de la modifier change.</summary>
    [RelayCommand]
    private void IncrementExperienceGained(WarriorOutcomeRow row) => row.ExperienceGainedText = (row.ExperienceGained + 1).ToString();

    [RelayCommand]
    private void DecrementExperienceGained(WarriorOutcomeRow row)
    {
        if (row.ExperienceGained > 0) row.ExperienceGainedText = (row.ExperienceGained - 1).ToString();
    }

    /// <summary>Bloque le passage à l'étape suivante tant qu'un jet visible de l'étape courante est vide
    /// ou invalide - seules les étapes Blessure et Progression ont des jets à valider (Résultat/Hors de
    /// combat/Expérience/Trésor n'en ont pas). Pose RollError/MultipleInjuryCountError sur chaque jet
    /// fautif (affiché sous le champ, voir XAML) plutôt qu'un message global - l'erreur s'efface
    /// d'elle-même dès que le joueur corrige la saisie (voir WarriorOutcomeRow/InjurySubRollEntry/
    /// AdvanceRollEntry.OnManualRollChanged), donc jamais recalculée ici pour les jets déjà valides.</summary>
    private bool ValidateCurrentStep()
    {
        return Current.Kind switch
        {
            StepKind.Injury => ValidateInjuryStep(CurrentInjuryWarrior!),
            StepKind.PitFight => ValidatePitFightStep(),
            StepKind.Captives => ValidateCaptivesStep(),
            StepKind.Advance => ValidateAdvanceStep(CurrentAdvanceRolls ?? Enumerable.Empty<AdvanceRollEntry>()),
            StepKind.ExplorationRoll => ValidateExplorationRollStep(),
            StepKind.ExplorationResult => ValidateExplorationResultStep(),
            StepKind.AvailableVeterans => ValidateAvailableVeteransStep(),
            StepKind.RareItems => ValidateRareItemsStep(),
            StepKind.RareItemPurchase => ValidateRareItemPurchaseStep(),
            StepKind.HiredSwords => ValidateHiredSwordsStep(),
            StepKind.DramatisPersonae => ValidateDramatisPersonaeStep(),
            StepKind.PairEngagement => ValidatePairEngagementStep(),
            StepKind.PairDuel => ValidatePairDuelStep(),
            StepKind.EquipmentTrading => !IsTreasuryBlocked,
            StepKind.RecruitHeroDetail => ValidateRecruitHeroDetailStep(CurrentRecruitHeroSlot!),
            StepKind.RecruitVeteranTopUp => !IsTreasuryBlocked,
            StepKind.RecruitHenchmenEquipment => !IsTreasuryBlocked,
            StepKind.RecruitHenchmenNames => ValidateRecruitHenchmenNamesStep(),
            _ => true
        };
    }

    private static bool CheckRoll(bool isMissing, Action setError)
    {
        if (isMissing) setError();
        return !isMissing;
    }

    /// <summary>Retour en arrière (2026-09-25, retour utilisateur - "reset l'étape si on clique sur
    /// précédent", version ciblée retenue) : quitter à reculons une étape dont les décisions visent une
    /// ligne précise de la réserve les annule, pour qu'un changement plus haut dans le wizard ne les
    /// laisse jamais pointer vers un objet disparu - Réallouer (tous ses déplacements, voir
    /// UndoReallocation) et Achat/Vente (les ventes seulement, les achats ne dépendent pas de la réserve).
    /// Le Recrutement n'est jamais effacé : ce qu'une recrue prend dans la réserve est recalculé en direct
    /// (ConsumeRecruitment/RecruitPickCost).</summary>
    [RelayCommand]
    private async Task Back()
    {
        if (StepIndex <= 0) return;

        switch (Current.Kind)
        {
            case StepKind.EquipmentReallocation:
                UndoReallocation();
                break;
            case StepKind.EquipmentTrading when PendingSales.Count > 0:
                PendingSales.Clear();
                NotifyTreasuryChanged();
                break;
        }

        await ChangeStepAsync(StepIndex - 1);
    }

    // Étape "Hors de combat" : un Héros (toujours HeadCount 1) se coche/décoche, mais un groupe
    // d'Hommes de main compte plusieurs figurines - le jet de Blessure Grave se fait par figurine
    // hors de combat, pas une fois pour tout le groupe (règle confirmée par l'utilisateur, 2026-08-17).
    // Ces deux commandes pilotent le stepper +/- du groupe, borné à [0, HeadCount] ; le clic est la
    // seule voie d'entrée (pas de saisie libre), donc pas de validation nécessaire ici.
    [RelayCommand]
    private void IncrementOutOfAction(WarriorOutcomeRow row) =>
        row.OutOfActionCount = Math.Min(row.Warrior.HeadCount, row.OutOfActionCount + 1);

    [RelayCommand]
    private void DecrementOutOfAction(WarriorOutcomeRow row) =>
        row.OutOfActionCount = Math.Max(0, row.OutOfActionCount - 1);

    /// <summary>Chip tapable sur le nom d'un guerrier DÉJÀ ACTIF, réutilisée par plusieurs étapes du
    /// wizard (Renvoyer/Vétérans/Hors de combat/Blessure/Progression/Expérience) - 2026-09-23, retour
    /// utilisateur "plutôt que d'avoir un label ou une chip non interagissable, ça serait top de pouvoir
    /// cliquer sur une chip qui nous révèle le détail du personnage". Distinct de
    /// EndOfGamePageViewModel.Recruitment.cs's ShowWarriorDetail (archétype candidat, pas encore un vrai
    /// Warrior) - voir WarriorRecapDialogViewModel's own doc pour la distinction complète.</summary>
    [RelayCommand]
    private Task ShowWarriorRecap(WarriorOutcomeRow row) =>
        _detailDialogs.ShowWarriorDetailDialogAsync(row.Warrior, row.ArchetypeName, row.SpecialRules);

    /// <summary>"Annuler"/flèche retour d'en-tête/geste OS (voir EndOfGamePage.xaml.cs's
    /// OnBackButtonPressed) - seul changement de comportement observable de tout le passage en page
    /// Shell (2026-09-22, décision utilisateur explicite) : une dialog fermée par le système équivalait
    /// silencieusement à Annuler (DialogStack.ModalPopped) ; une page Shell n'a pas cet équivalent
    /// gratuit. Retour silencieux tant que rien n'a été saisi (StepIndex == 0, même seuil que
    /// CanGoBack) ; sinon confirmation avant de perdre la saisie du wizard (jamais persistée avant
    /// FinishAsync de toute façon, donc rien n'est réellement perdu côté données - juste la saisie en
    /// cours).</summary>
    [RelayCommand]
    private async Task Exit()
    {
        if (StepIndex > 0 && !await ConfirmAsync(Loc["EndOfGameExitConfirmTitle"], Loc["EndOfGameExitConfirmMessage"]))
            return;

        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Étape "Récapitulatif", bouton Terminer - remplace l'ancien Close(true)/DialogViewModel
    /// depuis le passage en page Shell (2026-09-22) : plus de Task&lt;bool&gt; à faire remonter à
    /// l'appelant, ce ViewModel lance lui-même tout le pipeline Apply*Async (déplacé depuis
    /// WarbandDetailViewModel.EndOfGame.cs, voir EndOfGamePageViewModel.Apply.cs) puis navigue en
    /// arrière - WarbandDetailPage se rafraîchit tout seul au retour (voir son OnAppearing). Voir
    /// chaque Apply*Async's own doc pour les contraintes d'ordre encore pertinentes (statut/effectif) ;
    /// la réserve n'en impose plus aucune, écrite une seule fois à la fin (ApplyReserveAsync).</summary>
    [RelayCommand]
    private async Task Finish()
    {
        if (Warband is null) return;

        await Loading.RunAsync(async () =>
        {
            var language = LocalizationService.Instance.Language;
            var sentences = new List<string> { string.Format(Loc["HistoryResultSentence"], SelectedResult) };

            // Réserve finale et consommation des vétérans figées AVANT le pipeline : les Apply*Async
            // mutent des guerriers (équipement, effectif, XP) dont ces deux calculs dépendent. La réserve
            // n'est écrite en base qu'une fois, tout à la fin (ApplyReserveAsync) - aucune étape ci-dessous
            // ne touche plus WarbandEquipment, d'où la fin des contraintes d'ordre liées à la réserve.
            var finalReserve = BuildReserve(ReserveStage.Final);
            var topUpPlans = PlanTopUps().ToDictionary(kv => kv.Key, kv => (Plan: kv.Value, Breakdown: GetTopUpBreakdown(kv.Key)));
            var servedReservePicks = ServedReservePicks();

            await ApplyScenarioRewardsAsync(sentences);
            await ApplyExplorationOutcomeAsync(_englishEquipmentCatalog, _equipmentItemsByEnglishName, _englishSpecialRulesCatalog, sentences);
            await ApplyWarriorOutcomesAsync(language, sentences);
            await ApplyCapturedEnemiesAsync(_warriorArchetypesByEnglishName, sentences);
            await ApplyWyrdstoneSaleAsync(sentences);
            ApplyAvailableVeterans(sentences);
            await ApplyRareItemSearchAsync(_localizedEquipmentCatalog, sentences);
            // Délai de re-recherche (Aenur/Ulli & Marquand) : reste d'abord toute trace de cooldown
            // PRÉEXISTANTE (posée à une Fin de Partie antérieure) - le picker de CETTE Fin de Partie les
            // a déjà exclus, donc atteindre ce point veut dire "une bataille a été jouée sans eux",
            // satisfaisant la condition. Doit rester AVANT ApplyWandererDeparturesAsync, qui peut poser
            // un NOUVEAU cooldown pour un départ de CETTE Fin de Partie.
            await _warbandService.ClearAllDramatisPersonaCooldownsAsync(Warband.Id);
            await ApplyWandererDeparturesAsync(sentences);
            await ApplyDramatisPersonaUpkeepAsync(sentences);
            await ApplyPairEquipmentSeizureIfNeededAsync(sentences);
            await ApplyPairDuelIfNeededAsync(sentences);
            await ApplyHiredSwordUpkeepAsync(sentences);
            // Doit rester APRÈS ApplyWarriorOutcomesAsync (plus haut) : les morts de CETTE bataille
            // réduisent déjà Warrior.HeadCount avant qu'on reclampe DismissCount contre l'effectif RÉEL
            // (voir ApplyDismissalsAsync's own doc).
            await ApplyDismissalsAsync(sentences);
            await ApplyEquipmentTradingAsync(sentences);
            await ApplyRecruitmentAsync(servedReservePicks, sentences);
            await ApplyHenchmanRecruitmentAsync(topUpPlans, servedReservePicks, sentences);
            // Doit rester APRÈS ApplyWarriorOutcomesAsync : cette dernière resynchronise Warrior.Status
            // depuis l'étape Blessure (Actif/Mort) et écraserait Sick si elle passait avant (bug du
            // 2026-08-18) - invariant explicite ici plutôt qu'implicite dans l'ordre du code.
            // previouslySickWarriors vient de _allActiveWarriorRows, un instantané figé pris dans
            // InitializeAsync avant toute interaction du joueur avec ce wizard (même snapshot que
            // l'ancien previouslySickWarriors, capturé au tout début de WarbandDetailViewModel.
            // EndOfGame() avant ce refactor) - son contenu ne change jamais en cours de wizard, donc le
            // recalculer ici plutôt qu'au tout début produit exactement le même résultat.
            var previouslySickWarriors = _allActiveWarriorRows.Where(r => r.Warrior.Status == WarriorStatus.Sick).ToList();
            await ApplySicknessLifecycleAsync(previouslySickWarriors);
            // Réallouer l'équipement : côté Héros seulement (la Réserve est couverte par ApplyReserveAsync).
            await ApplyEquipmentReallocationAsync(sentences);
            await ApplyReserveAsync(finalReserve);

            // La partie est terminée : redonne la main à "Lancer la partie" sur la fiche de bande (voir
            // Warband.GameInProgress) - sans effet si elle n'avait jamais été lancée.
            Warband.GameInProgress = false;
            await _warbandService.SaveWarbandAsync(Warband);

            await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));
        });

        await Shell.Current.GoToAsync("..");
    }
}
