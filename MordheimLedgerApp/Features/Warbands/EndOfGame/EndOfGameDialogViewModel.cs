using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

using MordheimLedgerApp.Features.Warbands;

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
public partial class EndOfGameDialogViewModel : DialogViewModel<bool>
{
    private readonly ISkillPickerService _skillPicker;
    private readonly IDetailDialogService _detailDialogs;
    private readonly ILibraryService _libraryService;
    private readonly int _warbandArchetypeId;

    /// <summary>English WarbandArchetype.Name of the warband playing this game (e.g. "Skaven of Clan
    /// Eshin") - needed alongside _warbandArchetypeId because a Groupe B "conditional on warband type"
    /// Exploration branch (Core.Rules.ExplorationOutcomeResolver.ResolveWarbandOutcome) matches by name,
    /// not Id (see ExplorationOutcome.RestrictedToWarbandArchetypeNames - a plain string reference, same
    /// idiom as EquipmentItemName, since this is fixed rulebook content with no editor).</summary>
    private readonly string _warbandArchetypeName;

    /// <summary>Captured once at dialog construction (see Warband.PendingExplorationBonusDie) - read by
    /// ExplorationDiceCount, never reassigned mid-wizard: the flag itself is only cleared on the Warband
    /// once this Fin de Partie is actually saved (WarbandDetailViewModel.EndOfGame).</summary>
    private readonly bool _pendingExplorationBonusDie;

    /// <summary>Captured once at dialog construction (see Warband.HasCatacombReroll) - permanent, unlike
    /// _pendingExplorationBonusDie, so it's only ever read to show an informational reminder in the
    /// Exploration roll step (ShowCatacombRerollReminder), never cleared/consumed.</summary>
    private readonly bool _hasCatacombReroll;

    /// <summary>Snapshot of Warband.Treasury at dialog-open time (see EquippedHenchmanTreasuryAfter,
    /// Prisonniers' "autres bandes" branch) - the wizard never writes to the real Warband mid-dialog
    /// (only WarbandDetailViewModel.EndOfGame does, at Save), so this stays a plain frozen number for
    /// the affordability preview rather than a live reference.</summary>
    private readonly int _currentTreasury;

    private readonly List<ExplorationResult> _explorationResults;

    /// <summary>Nom anglais -> EquipmentItem résolu dans la langue courante, pour l'unique champ de ce
    /// wizard qui référence le catalogue Équipement par nom anglais brut plutôt que par Id
    /// (ExplorationOutcome.EquipmentItemName, voir sa doc) - sans ça, "Axe" s'affichait tel quel même en
    /// français. Construit une seule fois par l'appelant (WarbandDetailViewModel.EndOfGame) plutôt que
    /// refait à chaque résolution de branche. L'item entier (pas juste son nom) permet d'afficher un
    /// vrai ChipView tapable (icône de catégorie + popup détail via _detailDialogs) plutôt qu'un simple
    /// Label - même langage d'interaction que le reste de l'app pour toute référence Équipement.</summary>
    private readonly IReadOnlyDictionary<string, EquipmentItem> _equipmentItemsByEnglishName;

    /// <summary>Même idée que _equipmentItemsByEnglishName, pour ExplorationOutcome.MaterialRuleName (ex.
    /// "Ornate Weapon") - permet au ChipView d'afficher "Épée (O)" comme n'importe quel objet en Gromril/
    /// Ithilmar (voir WarbandEquipment.NameDisplay) plutôt que le nom nu de l'item.</summary>
    private readonly IReadOnlyDictionary<string, SpecialRule> _specialRulesByEnglishName;

    /// <summary>Nom anglais -> WarriorArchetype résolu dans la langue courante, pour
    /// ExplorationOutcome.GrantsFreeHenchmanArchetypeName (ex. "Zombie", Traînard) - même besoin que
    /// _equipmentItemsByEnglishName, mais limité aux archétypes de LA bande jouée (une branche
    /// conditionnée à une bande ne référence jamais l'archétype d'une autre).</summary>
    private readonly IReadOnlyDictionary<string, WarriorArchetype> _warriorArchetypesByEnglishName;

    /// <summary>Nom anglais -> Id de compétence, pour résoudre EquipmentItem.GrantsSpecificSkillName
    /// (voir Core.Rules.SkillEligibility.EffectiveExtraSkillNames) vers les ids que _skillPicker attend -
    /// le picker travaille sur son propre catalogue localisé, ce dictionnaire ne sert qu'à traverser la
    /// frontière anglais->id une fois, ici, plutôt qu'à chaque PickAdvanceSkill.</summary>
    private readonly IReadOnlyDictionary<string, int> _skillIdsByEnglishName;

    protected override bool CancelResult => false;

    public ObservableCollection<string> ResultOptions { get; } = new();
    public ObservableCollection<WarriorOutcomeRow> WarriorRows { get; }

    [ObservableProperty]
    private string selectedResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResultStep))]
    [NotifyPropertyChangedFor(nameof(IsOutOfActionStep))]
    [NotifyPropertyChangedFor(nameof(IsInjuryStep))]
    [NotifyPropertyChangedFor(nameof(IsExperienceStep))]
    [NotifyPropertyChangedFor(nameof(IsAdvanceStep))]
    [NotifyPropertyChangedFor(nameof(IsExplorationRollStep))]
    [NotifyPropertyChangedFor(nameof(IsExplorationResultStep))]
    [NotifyPropertyChangedFor(nameof(IsRecapStep))]
    [NotifyPropertyChangedFor(nameof(CurrentInjuryWarrior))]
    [NotifyPropertyChangedFor(nameof(InjuryProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CurrentAdvanceWarrior))]
    [NotifyPropertyChangedFor(nameof(CurrentAdvanceRolls))]
    [NotifyPropertyChangedFor(nameof(AdvanceProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int stepIndex;

    partial void OnStepIndexChanged(int value)
    {
        if (Current.Kind == StepKind.ExplorationRoll) SyncExplorationDice();
    }

    private enum StepKind { Result, OutOfAction, Injury, Experience, Advance, ExplorationRoll, ExplorationResult, Recap }

    /// <summary>IsExplorationAdvance distingue les DEUX passages possibles par StepKind.Advance pour un
    /// même guerrier : le premier (false), juste après Expérience, pour les paliers franchis par l'XP de
    /// bataille normale (WarriorOutcomeRow.MilestoneCount/AdvanceRolls) - place officielle de la
    /// Progression dans la séquence du livre ; le second (true), juste après Exploration, pour des
    /// paliers UNIQUEMENT atteints grâce à l'XP accordée par la table d'Exploration (Traînard/Prisonniers/
    /// Cimetière - WarriorOutcomeRow.ExplorationMilestoneCount/ExplorationAdvanceRolls), qui ne peut être
    /// détecté qu'une fois cette XP-là connue, donc après l'étape Exploration. Voir CurrentAdvanceRolls.</summary>
    private sealed record WizardStep(StepKind Kind, WarriorOutcomeRow? Warrior = null, bool IsExplorationAdvance = false);

    /// <summary>ExplorationResult ne s'ajoute que si un résultat a effectivement été déclenché par le
    /// jet précédent (au plus un, voir ExplorationChart.DetectMultiples) - même principe que les étapes
    /// Blessure/Progression qui n'apparaissent que pour les guerriers concernés, plutôt qu'une seule
    /// étape monolithique jet+résolution (retravaillé le 2026-08-17 suite à un retour explicite sur
    /// l'UX).</summary>
    private List<WizardStep> Steps
    {
        get
        {
            var steps = new List<WizardStep> { new(StepKind.Result), new(StepKind.OutOfAction) };
            steps.AddRange(WarriorRows.Where(r => r.IsOutOfAction).Select(r => new WizardStep(StepKind.Injury, r)));
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

    public bool IsResultStep => Current.Kind == StepKind.Result;
    public bool IsOutOfActionStep => Current.Kind == StepKind.OutOfAction;
    public bool IsInjuryStep => Current.Kind == StepKind.Injury;
    public bool IsExperienceStep => Current.Kind == StepKind.Experience;
    public bool IsAdvanceStep => Current.Kind == StepKind.Advance;
    public bool IsExplorationRollStep => Current.Kind == StepKind.ExplorationRoll;
    public bool IsExplorationResultStep => Current.Kind == StepKind.ExplorationResult;
    public bool IsRecapStep => Current.Kind == StepKind.Recap;

    /// <summary>Le seul guerrier affiché à l'étape Blessure courante - une étape par guerrier coché
    /// hors de combat, jamais une liste (voir la doc de classe).</summary>
    public WarriorOutcomeRow? CurrentInjuryWarrior => Current.Warrior;

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

    public EndOfGameDialogViewModel(IEnumerable<WarriorRow> activeWarriorRows, ISkillPickerService skillPicker, IDetailDialogService detailDialogs, ILibraryService libraryService, int warbandArchetypeId, string warbandArchetypeName, bool pendingExplorationBonusDie, bool hasCatacombReroll, int currentTreasury, List<ExplorationResult> explorationResults, IReadOnlyDictionary<string, EquipmentItem> equipmentItemsByEnglishName, IReadOnlyDictionary<string, SpecialRule> specialRulesByEnglishName, IReadOnlyDictionary<string, WarriorArchetype> warriorArchetypesByEnglishName, IReadOnlyDictionary<string, int> skillIdsByEnglishName, IReadOnlyList<Injury> injuryCatalog)
    {
        _skillPicker = skillPicker;
        _detailDialogs = detailDialogs;
        _libraryService = libraryService;
        _warbandArchetypeId = warbandArchetypeId;
        _warbandArchetypeName = warbandArchetypeName;
        _pendingExplorationBonusDie = pendingExplorationBonusDie;
        _hasCatacombReroll = hasCatacombReroll;
        _currentTreasury = currentTreasury;
        _explorationResults = explorationResults;
        _equipmentItemsByEnglishName = equipmentItemsByEnglishName;
        _warriorArchetypesByEnglishName = warriorArchetypesByEnglishName;
        _specialRulesByEnglishName = specialRulesByEnglishName;
        _skillIdsByEnglishName = skillIdsByEnglishName;

        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        selectedResult = ResultOptions[0];

        // Snapshot pour AdvanceRollEntry.CanPromote (promotion Homme de main -> Héros, jet 10-12) - voir
        // sa doc pour les limites acceptées (ne suit pas les promotions résolues plus tôt dans la même
        // Fin de Partie ; se base sur activeWarriorRows - un Héros Malade cette partie, donc absent
        // d'activeWarriorRows, n'est pas compté ici, sous-estimation mineure du plafond de 6 acceptée
        // plutôt que de faire remonter le roster complet de la bande jusqu'à ce ViewModel).
        var startingHeroCount = activeWarriorRows.Count(r => r.Warrior.IsHero);
        WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriorRows.Select(r =>
            new WarriorOutcomeRow(r.Warrior, r.RoleName, r.Warrior.GainsExperience, r.MagicSchools, startingHeroCount, injuryCatalog)));

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
            };
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (!ValidateCurrentStep()) return;
        if (StepIndex < Steps.Count - 1) StepIndex++;
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
            StepKind.Advance => ValidateAdvanceStep(CurrentAdvanceRolls ?? Enumerable.Empty<AdvanceRollEntry>()),
            StepKind.ExplorationRoll => ValidateExplorationRollStep(),
            StepKind.ExplorationResult => ValidateExplorationResultStep(),
            _ => true
        };
    }

    private static bool CheckRoll(bool isMissing, Action setError)
    {
        if (isMissing) setError();
        return !isMissing;
    }

    [RelayCommand]
    private void Back()
    {
        if (StepIndex > 0) StepIndex--;
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

    [RelayCommand]
    private void Save() => Close(true);
}
