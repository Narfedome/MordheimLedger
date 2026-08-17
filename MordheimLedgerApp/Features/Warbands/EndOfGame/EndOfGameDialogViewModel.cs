using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

using MordheimLedgerApp.Features.Warbands;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>
/// Wizard qui suit la "Séquence d'après-bataille" du livre de règles : Résultat, Hors de combat,
/// une étape Blessure par guerrier coché hors de combat, Expérience, Progression, Exploration
/// (chapitre "Revenus" - jet + résolution de la table d'Exploration, voir Core.Rules.ExplorationChart
/// et Models.Library.ExplorationResult/ExplorationOutcome), Récapitulatif - dans cet ordre (Blessures
/// Graves puis Expérience puis Revenus, à faire "devant témoin" juste après la partie ; le reste de la
/// séquence - vente de pierre de sorcière, disponibilité des vétérans, personnages spéciaux, achats...
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
    private readonly int _warbandArchetypeId;
    private readonly List<ExplorationResult> _explorationResults;

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

    private sealed record WizardStep(StepKind Kind, WarriorOutcomeRow? Warrior = null);

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

    public string AdvanceProgressLabel
    {
        get
        {
            var warrior = CurrentAdvanceWarrior;
            if (warrior is null) return string.Empty;

            var withMilestone = WarriorRows.Where(r => r.HasMilestone).ToList();
            var index = withMilestone.IndexOf(warrior);
            return index < 0 ? string.Empty : string.Format(Loc["EndOfGameAdvanceProgressLabel"], index + 1, withMilestone.Count);
        }
    }

    public bool CanGoBack => StepIndex > 0;
    public bool IsLastStep => StepIndex >= Steps.Count - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], StepIndex + 1, Steps.Count);

    // --- Étape Exploration (Séquence d'après-bataille, "Revenus") ------------------------------
    //
    // Un D6 par Héros survivant sans être hors de combat (jamais les Hommes de main) + 1D6 si la
    // bande a gagné, plafonné à 6 dés (voir Core.Rules.ExplorationChart - règle du livre confirmée
    // par l'utilisateur le 2026-08-17). Le nombre de dés ne peut varier qu'entre Result/HorsDeCombat
    // (déjà résolus quand on atteint cette étape) et le moment où on l'atteint, donc SyncExplorationDice
    // n'a besoin d'être appelée qu'en y entrant (OnStepIndexChanged) plutôt qu'à chaque frappe.
    public int SurvivingHeroCount => WarriorRows.Count(r => r.IsHero && !r.IsOutOfAction);
    public bool WonLastGame => ResultOptions.Count > 0 && SelectedResult == ResultOptions[0];
    public int ExplorationDiceCount => ExplorationChart.ComputeDiceCount(SurvivingHeroCount, WonLastGame);

    public ObservableCollection<ExplorationDieEntry> ExplorationDice { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplorationResult))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationSubRoll))]
    [NotifyPropertyChangedFor(nameof(ExplorationNoteText))]
    // Steps() gagne/perd son étape ExplorationResult selon cette valeur (voir Steps) - le total affiché
    // par StepLabel et la visibilité du bouton "Suivant"/"Enregistrer" (IsLastStep) doivent donc suivre
    // en direct, pas seulement au prochain changement de StepIndex/WarriorRows (même classe de bug que
    // le HasExplorationResult manquant corrigé plus tôt : sans ces deux lignes, l'UI resterait figée sur
    // l'ancien total tant que rien d'autre ne la rafraîchit).
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    private ExplorationResult? triggeredExplorationResult;

    /// <summary>Un seul résultat peut être déclenché par jet (voir ExplorationChart.DetectMultiples) -
    /// null tant que tous les dés ne sont pas renseignés, ou si aucun doublon n'a été trouvé (issue
    /// normale de la table, pas une erreur).</summary>
    public bool HasExplorationResult => TriggeredExplorationResult is not null;

    /// <summary>Un résultat à plusieurs branches mutuellement exclusives (Groupe A, ex. Cadavre : 1-2
    /// po, 3 Dague, 4 Hache...) se départage par un sous-jet D6 - un résultat à une seule branche (ex.
    /// Masures en Ruine) ou dont aucune Outcome n'a de sous-jet n'en a pas besoin. Les résultats à choix
    /// du joueur (Groupe B, RollsIndependently) ne sont pas encore gérés par cette étape (à venir, voir
    /// le plan de séquencement) - ShowExplorationSubRoll reste false pour eux pour l'instant.</summary>
    public bool ShowExplorationSubRoll => TriggeredExplorationResult is { RollsIndependently: false } r
        && r.Outcomes.Count > 1 && r.Outcomes.All(o => o.SubRollMin.HasValue);

    [ObservableProperty]
    private string explorationSubRoll = string.Empty;

    [ObservableProperty]
    private string? explorationSubRollError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorationGold))]
    [NotifyPropertyChangedFor(nameof(IsExplorationItem))]
    [NotifyPropertyChangedFor(nameof(IsExplorationWyrdstone))]
    [NotifyPropertyChangedFor(nameof(IsExplorationNone))]
    [NotifyPropertyChangedFor(nameof(ExplorationNoteText))]
    private ExplorationOutcome? resolvedExplorationOutcome;

    /// <summary>Texte affiché pour une branche Kind.None (voir IsExplorationNone) - le Note de la
    /// branche retenue (ex. "Skavens : vente aux agents du Clan Eshin"), ou à défaut le nom du résultat
    /// déclenché si la branche n'en porte pas.</summary>
    public string ExplorationNoteText => ResolvedExplorationOutcome?.Note ?? TriggeredExplorationResult?.Name ?? string.Empty;

    public bool IsExplorationGold => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold;
    public bool IsExplorationItem => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Item;
    public bool IsExplorationWyrdstone => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Wyrdstone;

    /// <summary>Branche retenue sans effet trésorerie/inventaire (ex. Traînard/"autres bandes",
    /// Charrette Renversée 5-6) - reste purement informatif (Note ou Description du résultat), juste
    /// consigné dans l'Historique à la sauvegarde (voir WarbandDetailViewModel.EndOfGame) plutôt que
    /// silencieusement perdu.</summary>
    public bool IsExplorationNone => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.None;

    /// <summary>Montant d'or résolu (formule roulée automatiquement dès la branche retenue) - reste un
    /// Entry modifiable comme tous les autres jets de ce wizard, un jet physique du joueur prime
    /// toujours sur le tirage automatique.</summary>
    [ObservableProperty]
    private string explorationGoldAmount = string.Empty;

    [ObservableProperty]
    private string explorationItemQuantity = string.Empty;

    /// <summary>Même principe que ExplorationGoldAmount, pour une branche Kind.Wyrdstone (ex. Puits,
    /// Bâtiment Éventré, La Fosse) - GoldFormula est réutilisé tel quel comme formule de pierres de
    /// sorcière (voir ExplorationOutcome.GoldFormula).</summary>
    [ObservableProperty]
    private string explorationWyrdstoneAmount = string.Empty;

    private void SyncExplorationDice()
    {
        var count = ExplorationDiceCount;
        while (ExplorationDice.Count < count)
        {
            var entry = new ExplorationDieEntry(ExplorationDice.Count + 1);
            entry.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExplorationDieEntry.ManualRoll)) ResolveExplorationResult();
            };
            ExplorationDice.Add(entry);
        }
        while (ExplorationDice.Count > count)
            ExplorationDice.RemoveAt(ExplorationDice.Count - 1);
    }

    /// <summary>Recalcule le résultat déclenché dès que tous les dés d'Exploration sont renseignés -
    /// réinitialise tout l'état en aval (sous-jet, branche résolue, or/objet) à chaque nouveau tirage
    /// plutôt que de laisser une résolution obsolète affichée.</summary>
    private void ResolveExplorationResult()
    {
        TriggeredExplorationResult = null;
        ExplorationSubRoll = string.Empty;
        ExplorationSubRollError = null;
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;

        if (ExplorationDice.Any(d => d.Value is null)) return;

        var multiple = ExplorationChart.DetectMultiples(ExplorationDice.Select(d => d.Value!.Value).ToList());
        if (multiple is null) return;

        TriggeredExplorationResult = _explorationResults
            .FirstOrDefault(r => r.DiceCount == multiple.Value.DiceCount && r.Value == multiple.Value.Value);

        // Une seule branche sans sous-jet (ex. Masures en Ruine) se résout tout de suite, pas besoin
        // d'un jet supplémentaire.
        if (TriggeredExplorationResult is { Outcomes.Count: 1 } single && single.Outcomes[0].SubRollMin is null)
            ApplyResolvedOutcome(single.Outcomes[0]);
    }

    partial void OnExplorationSubRollChanged(string value)
    {
        ExplorationSubRollError = null;
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;

        if (TriggeredExplorationResult is null || !int.TryParse(value, out var roll)) return;

        var outcome = TriggeredExplorationResult.Outcomes
            .FirstOrDefault(o => o.SubRollMin.HasValue && roll >= o.SubRollMin && roll <= o.SubRollMax);
        if (outcome is not null) ApplyResolvedOutcome(outcome);
    }

    private void ApplyResolvedOutcome(ExplorationOutcome outcome)
    {
        ResolvedExplorationOutcome = outcome;
        if (outcome.Kind == ExplorationOutcomeKind.Gold && outcome.GoldFormula is not null)
            ExplorationGoldAmount = DiceFormula.Roll(outcome.GoldFormula).ToString();
        else if (outcome.Kind == ExplorationOutcomeKind.Item && outcome.ItemQuantityFormula is not null)
            ExplorationItemQuantity = DiceFormula.Roll(outcome.ItemQuantityFormula).ToString();
        else if (outcome.Kind == ExplorationOutcomeKind.Wyrdstone && outcome.GoldFormula is not null)
            ExplorationWyrdstoneAmount = DiceFormula.Roll(outcome.GoldFormula).ToString();
        // Kind.None : rien à tirer, ResolvedExplorationOutcome suffit (voir IsExplorationNone) - juste
        // consigné dans l'Historique à la sauvegarde.
    }

    [RelayCommand]
    private void AutoRollExplorationDie(ExplorationDieEntry entry) => entry.ManualRoll = ExplorationChart.RollDie().ToString();

    [RelayCommand]
    private void AutoRollExplorationSubRoll() => ExplorationSubRoll = ExplorationChart.RollDie().ToString();

    public EndOfGameDialogViewModel(IEnumerable<WarriorRow> activeWarriorRows, ISkillPickerService skillPicker, IDetailDialogService detailDialogs, int warbandArchetypeId, List<ExplorationResult> explorationResults)
    {
        _skillPicker = skillPicker;
        _detailDialogs = detailDialogs;
        _warbandArchetypeId = warbandArchetypeId;
        _explorationResults = explorationResults;

        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        selectedResult = ResultOptions[0];

        WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriorRows.Select(r =>
            new WarriorOutcomeRow(r.Warrior, r.RoleName, r.Warrior.GainsExperience)));

        // Le nombre d'étapes dépend de IsOutOfAction (étapes Blessure) et de HasMilestone (étapes
        // Progression) - Steps recalcule ça à chaque accès, mais on rafraîchit quand même StepLabel/
        // IsLastStep tout de suite pour que le joueur voie le compte à jour pendant qu'il coche des
        // guerriers ou saisit des PX, plutôt que d'attendre le prochain Next/Back.
        foreach (var row in WarriorRows)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WarriorOutcomeRow.IsOutOfAction) or nameof(WarriorOutcomeRow.ExperienceGained))
                {
                    OnPropertyChanged(nameof(StepLabel));
                    OnPropertyChanged(nameof(IsLastStep));
                }
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
            StepKind.Advance => ValidateAdvanceStep(CurrentAdvanceWarrior!),
            StepKind.ExplorationRoll => ValidateExplorationRollStep(),
            StepKind.ExplorationResult => ValidateExplorationResultStep(),
            _ => true
        };
    }

    private bool ValidateInjuryStep(WarriorOutcomeRow row)
    {
        var valid = true;

        if (row.Warrior.IsHero)
        {
            valid &= CheckRoll(string.IsNullOrWhiteSpace(row.InjuryResultText), () => row.RollError = Loc["EndOfGameRollRequired"]);

            if (row.ShowMultipleInjuriesSection)
            {
                valid &= CheckRoll(row.MultipleInjuryRolls.Count == 0, () => row.MultipleInjuryCountError = Loc["EndOfGameRollRequired"]);
                foreach (var sub in row.MultipleInjuryRolls)
                    valid &= CheckRoll(string.IsNullOrWhiteSpace(sub.InjuryResultText), () => sub.RollError = Loc["EndOfGameRollRequired"]);
            }
        }
        else
        {
            foreach (var figure in row.FigureInjuryRolls)
                valid &= CheckRoll(string.IsNullOrWhiteSpace(figure.InjuryResultText), () => figure.RollError = Loc["EndOfGameRollRequired"]);
        }

        return valid;
    }

    private bool ValidateAdvanceStep(WarriorOutcomeRow row)
    {
        var valid = true;
        foreach (var advance in row.AdvanceRolls)
            valid &= CheckRoll(string.IsNullOrWhiteSpace(advance.ResultText), () => advance.RollError = Loc["EndOfGameRollRequired"]);
        return valid;
    }

    /// <summary>Bloque tant que les dés d'Exploration ne sont pas tous renseignés, et - si le résultat
    /// déclenché a plusieurs branches à choix exclusif (Groupe A, ex. Cadavre) - tant que le sous-jet
    /// qui les départage n'a pas résolu de branche. Un jet qui ne déclenche rien (pas de doublon) ou un
    /// résultat sans sous-jet (branche unique, ex. Masures en Ruine) n'a rien de plus à valider - dans
    /// ces deux cas, l'étape ExplorationResult n'existe même pas (voir Steps).</summary>
    private bool ValidateExplorationRollStep()
    {
        var valid = true;
        foreach (var die in ExplorationDice)
            valid &= CheckRoll(die.Value is null, () => die.RollError = Loc["EndOfGameRollRequired"]);
        return valid;
    }

    private bool ValidateExplorationResultStep()
    {
        if (!ShowExplorationSubRoll) return true;
        return CheckRoll(ResolvedExplorationOutcome is null, () => ExplorationSubRollError = Loc["EndOfGameRollRequired"]);
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

    // Lance les dés à la place du joueur (D66 pour un Héros, D6 pour un Homme de main - deux tables
    // totalement différentes, voir SeriousInjuryTable/HenchmanInjuryTable) - le champ ManualRoll reste
    // modifiable ensuite si le joueur préfère un jet physique. Dans les deux cas (dé ou saisie
    // manuelle), la résolution texte + ApplyInjuryRoll se fait automatiquement dès que ManualRoll
    // contient un jet complet et valide (voir WarriorOutcomeRow.OnManualRollChanged) et s'affiche tout
    // de suite sous le champ - plus de popup de confirmation après un clic sur le dé (décision
    // explicite du 2026-08-17, devenue redondante avec cet affichage automatique).
    [RelayCommand]
    private void AutoRoll(WarriorOutcomeRow row)
    {
        var roll = row.Warrior.IsHero ? SeriousInjuryTable.RollDice() : HenchmanInjuryTable.RollDice();
        row.ManualRoll = roll.ToString();
    }

    // Résultat "Blessures multiples" (16/21, Héros uniquement) : le joueur lance 1D6 pour savoir
    // combien de sous-jets faire sur cette même table (règle du livre : "Roll D6 times on this
    // table" = un nombre déterminé par 1D6, pas un compte fixe). Comme AutoRoll ci-dessus, une saisie
    // manuelle valide (1 à 6) peuple MultipleInjuryRolls toute seule (WarriorOutcomeRow.
    // OnMultipleInjuryCountRollChanged) - ce bouton ne fait que tirer le 1D6 à la place du joueur.
    [RelayCommand]
    private void AutoRollMultipleInjuryCount(WarriorOutcomeRow row) => row.MultipleInjuryCountRoll = Random.Shared.Next(1, 7).ToString();

    // Sert deux cas distincts qui partagent la même forme (voir la doc d'InjurySubRollEntry) : les
    // sous-jets "Blessures multiples" d'un Héros (D66, entry.IsHero true) et les jets par figurine d'un
    // groupe d'Hommes de main hors de combat (D6, entry.IsHero false). Même résolution automatique que
    // le jet principal (InjurySubRollEntry.OnManualRollChanged), y compris pour un résultat qui devrait
    // en théorie être relancé (Mort/Capturé/Blessures multiples, cf. livre, Héros uniquement) -
    // décision explicite : l'appli n'impose ni ne relance rien elle-même, le résultat du joueur est
    // accepté tel quel comme n'importe quel autre jet de cette table.
    [RelayCommand]
    private void AutoRollSubInjury(InjurySubRollEntry entry)
    {
        var roll = entry.IsHero ? SeriousInjuryTable.RollDice() : HenchmanInjuryTable.RollDice();
        entry.ManualRoll = roll.ToString();
    }

    // Un guerrier peut franchir plusieurs paliers d'un coup - chaque AdvanceRollEntry (une par palier,
    // voir WarriorOutcomeRow.SyncAdvanceRolls) est un jet 2D6 indépendant sur la table de progression,
    // même résolution automatique que AutoRoll mais purement descriptif : aucune stat n'est modifiée
    // automatiquement (les sous-jets 1D6 des résultats 6/8/9 et le choix CC/CT du 7 restent à résoudre
    // par le joueur, cf. HeroAdvanceTable/HenchmanAdvanceTable).
    [RelayCommand]
    private void AutoRollAdvance(AdvanceRollEntry entry)
    {
        var roll = entry.IsHero ? HeroAdvanceTable.RollDice() : HenchmanAdvanceTable.RollDice();
        entry.ManualRoll = roll.ToString();
    }

    // Résultat "Compétence" (voir HeroAdvanceTable.IsSkill) : le joueur choisit directement une
    // compétence existante de la Bibliothèque, comme le "+" Compétences de la carte guerrier -
    // rattachée au guerrier par WarbandDetailViewModel.EndOfGame à l'enregistrement du wizard, pas
    // tout de suite (même logique différée que les autres résultats de cette étape).
    [RelayCommand]
    private async Task PickAdvanceSkill(AdvanceRollEntry entry)
    {
        var row = WarriorRows.First(r => r.AdvanceRolls.Contains(entry));
        var skills = await _skillPicker.PickSkillAsync(_warbandArchetypeId, row.Warrior.WarriorArchetypeId,
            row.Warrior.AllowedSkillCategories);
        foreach (var skill in skills)
            entry.SelectedSkills.Add(skill);
    }

    [RelayCommand]
    private Task ShowSkillDetail(Skill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill);

    [RelayCommand]
    private void Save() => Close(true);
}

/// <summary>One row per active Warrior in the End of Game dialog — collects the outcome, the caller
/// (WarbandDetailViewModel) applies it via IWarbandService and builds the History sentence.</summary>
public partial class WarriorOutcomeRow : ObservableObject
{
    private readonly Dictionary<string, WarriorStatus> _statusByLabel = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public Warrior Warrior { get; }
    public string Name => Warrior.Name;

    /// <summary>The archetype's name (e.g. "Zombie", "Capitaine") shown instead of a plain Héros/Homme
    /// de main label on every step of this wizard - same value as WarriorRow.RoleName, passed in by
    /// the caller (WarbandDetailViewModel.EndOfGame already has it resolved for the roster).</summary>
    public string ArchetypeName { get; }

    /// <summary>Copie de Warrior.GainsExperience (donc, en amont, WarriorArchetype.GainsExperience -
    /// false pour les types comme Zombie, voir sa doc). Exclut ce guerrier de l'étape Expérience
    /// (ShowsInExperienceStep) et donc, en cascade, de la Progression (HasMilestone).</summary>
    public bool GainsExperience { get; }

    /// <summary>Un guerrier mort (étape Blessure) ou qui ne gagne jamais d'XP n'a rien à faire à
    /// l'étape Expérience - retiré de la liste plutôt que juste grisé/désactivé.</summary>
    public bool ShowsInExperienceStep => !IsDead && GainsExperience;

    /// <summary>Saisie libre du PX gagné cette partie (scénario + survie + bonus outsider - non calculé,
    /// voir la doc de classe d'EndOfGameDialogViewModel) - texte plutôt qu'int pour que le champ parte
    /// vide au lieu d'afficher "0" par défaut, même motif que ManualRoll plus bas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ExperienceGained))]
    [NotifyPropertyChangedFor(nameof(MilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasMilestone))]
    private string experienceGainedText = string.Empty;

    partial void OnExperienceGainedTextChanged(string value) => SyncAdvanceRolls();

    public int ExperienceGained => int.TryParse(ExperienceGainedText, out var xp) ? xp : 0;

    public bool IsHero => Warrior.IsHero;
    public int HeadCount => Warrior.HeadCount;

    /// <summary>Nombre de figurines hors de combat à la fin de la partie - toujours 0 ou 1 pour un Héros
    /// (HeadCount vaut 1), mais peut monter jusqu'à HeadCount pour un groupe d'Hommes de main : chaque
    /// figurine hors de combat a son propre jet de Blessure Grave (règle confirmée, voir
    /// SyncFigureInjuryRolls) - pas un seul jet pour tout le groupe. Piloté par le stepper +/- de
    /// l'étape "Hors de combat" (IncrementOutOfAction/DecrementOutOfAction) pour un groupe, par la case
    /// à cocher IsOutOfAction pour un Héros.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutOfAction))]
    [NotifyPropertyChangedFor(nameof(OutOfActionLabel))]
    private int outOfActionCount;

    /// <summary>Wrapper booléen pour la case à cocher d'un Héros (HeadCount toujours 1, donc 0/1
    /// suffit) - un groupe d'Hommes de main utilise directement OutOfActionCount via le stepper, jamais
    /// cette case.</summary>
    public bool IsOutOfAction
    {
        get => OutOfActionCount > 0;
        set => OutOfActionCount = value ? 1 : 0;
    }

    public string OutOfActionLabel => $"{OutOfActionCount}/{HeadCount}";

    /// <summary>OutOfActionCount change (case cochée/décochée pour un Héros, stepper +/- pour un groupe
    /// d'Hommes de main) : ce guerrier n'a plus d'étape Blessure dans le wizard si la valeur retombe à 0
    /// (voir EndOfGameDialogViewModel.Steps), donc plus rien à y montrer. Héros et groupe divergent
    /// ensuite : un Héros efface son unique jet (ManualRoll/InjuryResultText/Blessures multiples/statut
    /// dérivé) ; un groupe resynchronise juste FigureInjuryRolls sur le nouveau compte (peut aussi
    /// grandir, contrairement au cas Héros qui ne vaut jamais plus que 1).</summary>
    partial void OnOutOfActionCountChanged(int value)
    {
        if (Warrior.IsHero)
        {
            if (value > 0) return;

            ManualRoll = string.Empty;
            InjuryResultText = string.Empty;
            MultipleInjuryCountRoll = string.Empty;
            if (MultipleInjuryRolls.Count > 0)
            {
                MultipleInjuryRolls.Clear();
                OnPropertyChanged(nameof(HasMultipleInjuryRolls));
            }
            SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == Warrior.Status).Key;
        }
        else
        {
            SyncFigureInjuryRolls();
        }
    }

    /// <summary>Le score D66 (Héros) ou D6 (Homme de main) - saisi à la main (jet physique) ou rempli
    /// par AutoRoll. Dès que la valeur est un jet complet et valide, InjuryResultText se résout tout
    /// seul (OnManualRollChanged) - pas de bouton "Voir le résultat" à cliquer après une saisie
    /// physique, décision explicite du 2026-08-17.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMultipleInjuriesSection))]
    private string manualRoll = string.Empty;

    /// <summary>Message affiché sous le champ ManualRoll si le joueur essaie de passer à l'étape
    /// suivante (EndOfGameDialogViewModel.Next) sans jet valide - jamais posé par une simple frappe,
    /// seulement par cette validation ; effacé dès que le jet redevient valide (ci-dessous), pas
    /// seulement au prochain essai de Suivant.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        InjuryResultText = string.Empty;
        if (int.TryParse(value, out var roll)) ResolveInjuryResult(roll);
        if (!string.IsNullOrWhiteSpace(InjuryResultText)) RollError = null;

        // Si le jet principal est refait vers un résultat qui n'est plus "Blessures multiples", les
        // sous-jets précédemment saisis n'ont plus de sens - on les efface plutôt que de les laisser
        // affichés pour un résultat qui ne les déclenche plus.
        if (!Warrior.IsHero || ShowMultipleInjuriesSection) return;
        if (MultipleInjuryCountRoll.Length == 0 && MultipleInjuryRolls.Count == 0) return;

        MultipleInjuryCountRoll = string.Empty;
        MultipleInjuryRolls.Clear();
        OnPropertyChanged(nameof(HasMultipleInjuryRolls));
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>Le jet/la classification (mort ou non) vivent dans Core.Rules, sans dépendance à la
    /// localisation - cette résolution de clé -> texte affiché reste ici côté tête MAUI (WarriorOutcomeRow
    /// a déjà accès à LocalizationService via _loc, comme pour SelectedStatusLabel).</summary>
    private void ResolveInjuryResult(int roll)
    {
        bool found;
        string key;
        found = Warrior.IsHero ? SeriousInjuryTable.TryGetTextKey(roll, out key) : HenchmanInjuryTable.TryGetTextKey(roll, out key);
        if (!found) return;

        InjuryResultText = _loc[key];
        ApplyInjuryRoll(roll);
    }

    /// <summary>Texte complet du résultat une fois consulté (via AutoRoll ou ShowInjuryResult) -
    /// c'est ce texte qui alimente la note du guerrier et la phrase d'Historique à la sauvegarde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string injuryResultText = string.Empty;

    /// <summary>Un jet 2D6 par palier d'XP franchi cette partie (voir SyncAdvanceRolls) - un guerrier
    /// qui saute plusieurs cases à bord épais d'un coup doit relancer autant de fois sur la table de
    /// progression (Campagne.md § Expérience). Distincte du jet de Blessure Grave (ManualRoll) : les
    /// deux peuvent coexister le même End of Game.</summary>
    public ObservableCollection<AdvanceRollEntry> AdvanceRolls { get; } = new();

    /// <summary>True dès que le jet principal (ManualRoll) donne "Blessures multiples" (16, 21,
    /// Héros uniquement) - pilote l'affichage du bloc "combien de sous-jets" (1D6) dans le XAML, avant
    /// même que ce 1D6 ait été tiré.</summary>
    public bool ShowMultipleInjuriesSection => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && SeriousInjuryTable.IsMultipleInjuries(roll);

    /// <summary>Le score du 1D6 tiré pour savoir combien de sous-jets faire - saisi à la main ou
    /// rempli par AutoRollMultipleInjuryCount. Dès que la valeur est 1 à 6, MultipleInjuryRolls se
    /// peuple tout seul (OnMultipleInjuryCountRollChanged), même principe que ManualRoll ci-dessus.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string multipleInjuryCountRoll = string.Empty;

    /// <summary>Même principe que RollError, pour le champ 1D6 "combien de sous-jets" plutôt que le jet
    /// principal.</summary>
    [ObservableProperty]
    private string? multipleInjuryCountError;

    partial void OnMultipleInjuryCountRollChanged(string value)
    {
        if (int.TryParse(value, out var count) && count is >= 1 and <= 6)
        {
            PopulateMultipleInjuryRolls(count);
            MultipleInjuryCountError = null;
        }
    }

    /// <summary>Un sous-jet D66 par point du 1D6 ci-dessus - peuplée par SetMultipleInjuryCount une
    /// fois ce 1D6 résolu. Vide tant qu'il ne l'est pas, et pour tout guerrier/résultat qui n'est pas
    /// concerné par les Blessures multiples.</summary>
    public ObservableCollection<InjurySubRollEntry> MultipleInjuryRolls { get; } = new();
    public bool HasMultipleInjuryRolls => MultipleInjuryRolls.Count > 0;

    /// <summary>Un jet D6 par figurine hors de combat dans un groupe d'Hommes de main (OutOfActionCount)
    /// - sans objet pour un Héros, qui utilise ManualRoll/InjuryResultText ci-dessus à la place (une
    /// seule figurine, un seul jet). Peuplée/resynchronisée par SyncFigureInjuryRolls à chaque
    /// changement d'OutOfActionCount.</summary>
    public ObservableCollection<InjurySubRollEntry> FigureInjuryRolls { get; } = new();

    /// <summary>Plus de saisie manuelle : uniquement modifié par ApplyInjuryRoll (résultat "Mort",
    /// jets 11-15) ou remis à l'état d'origine par OnIsOutOfActionChanged. Reste sur Warrior.Status
    /// (donc "Actif") pour tout le reste, y compris les rétablissements et le résultat "Blessures
    /// multiples" (16/21, ambigu tant que les sous-jets ne sont pas résolus).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(IsDead))]
    [NotifyPropertyChangedFor(nameof(ShowsInExperienceStep))]
    private string selectedStatusLabel = string.Empty;

    public WarriorStatus Status => _statusByLabel.GetValueOrDefault(SelectedStatusLabel, Warrior.Status);
    public bool IsDead => Status == WarriorStatus.Dead;

    /// <summary>Nombre de cases à bord épais franchies par les PX gagnés cette partie (voir
    /// ExperienceMilestones) - peut dépasser 1 si le guerrier cumule assez de PX pour sauter
    /// plusieurs paliers d'un coup, d'où AdvanceRolls plutôt qu'un jet unique.</summary>
    public int MilestoneCount => ExperienceMilestones.MilestonesCrossedCount(Warrior.IsHero, Warrior.Experience, Warrior.Experience + ExperienceGained);
    public bool HasMilestone => GainsExperience && MilestoneCount > 0;

    /// <summary>Héros et Hommes de main utilisent deux tables de Blessures Graves totalement
    /// différentes (D66 vs D6, voir SeriousInjuryTable/HenchmanInjuryTable) - ce placeholder garde la
    /// distinction visible sur l'étape Blessure de chaque guerrier (ArchetypeName, affiché à côté,
    /// n'en dit rien explicitement).</summary>
    public string RollPlaceholder => _loc[Warrior.IsHero ? "EndOfGameRollPh" : "EndOfGameHenchmanRollPh"];

    /// <summary>Ligne affichée à l'étape Récapitulatif - ne liste que ce qui a réellement changé.</summary>
    public string SummaryText
    {
        get
        {
            var parts = new List<string>();
            if (ExperienceGained != 0) parts.Add($"+{ExperienceGained} PX");
            if (Status != Warrior.Status) parts.Add(SelectedStatusLabel);
            if (!string.IsNullOrWhiteSpace(InjuryResultText)) parts.Add(InjuryResultText);
            foreach (var sub in MultipleInjuryRolls)
                if (!string.IsNullOrWhiteSpace(sub.InjuryResultText)) parts.Add(sub.InjuryResultText);
            foreach (var figure in FigureInjuryRolls)
                if (!string.IsNullOrWhiteSpace(figure.InjuryResultText)) parts.Add(figure.InjuryResultText);
            foreach (var advance in AdvanceRolls)
            {
                if (advance.SelectedSkills.Count > 0) parts.Add(advance.SelectedSkillsText);
                else if (!string.IsNullOrWhiteSpace(advance.ResultText)) parts.Add(advance.ResultText);
            }

            return parts.Count > 0
                ? $"{Name} : {string.Join(", ", parts)}"
                : string.Format(_loc["EndOfGameNoChange"], Name);
        }
    }

    public WarriorOutcomeRow(Warrior warrior, string archetypeName, bool gainsExperience)
    {
        Warrior = warrior;
        ArchetypeName = archetypeName;
        GainsExperience = gainsExperience;

        foreach (var status in new[] { WarriorStatus.Active, WarriorStatus.Dead })
            _statusByLabel[_loc[$"WarriorStatus{status}"]] = status;

        selectedStatusLabel = _statusByLabel.First(kv => kv.Value == warrior.Status).Key;
    }

    /// <summary>Appelé après un jet de Blessure Grave - passe le guerrier à Mort pour les résultats
    /// de mort sans équivoque (Héros : 11-15 sur la table D66 ; Homme de main : 1-2 sur la table D6,
    /// mécaniques totalement différentes). Le reste (rétablissements, blessures permanentes,
    /// "Blessures multiples"...) ne touche pas le Statut.</summary>
    public void ApplyInjuryRoll(int roll)
    {
        var isDeath = Warrior.IsHero ? SeriousInjuryTable.IsDeath(roll) : HenchmanInjuryTable.IsDeath(roll);
        if (isDeath)
            SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == WarriorStatus.Dead).Key;
    }

    /// <summary>(Re)peuple MultipleInjuryRolls avec exactement <paramref name="count"/> sous-jets (le
    /// résultat du 1D6 tiré pour savoir combien en faire, cf. règle "Blessures multiples") - remplace
    /// entièrement les entrées précédentes, un nouveau tirage du 1D6 recommence à zéro plutôt que de
    /// s'ajouter aux sous-jets déjà saisis. N'écrit pas MultipleInjuryCountRoll lui-même : appelée
    /// depuis OnMultipleInjuryCountRollChanged, qui réagit déjà à ce champ.</summary>
    private void PopulateMultipleInjuryRolls(int count)
    {
        MultipleInjuryRolls.Clear();
        for (var i = 1; i <= count; i++)
        {
            var entry = new InjurySubRollEntry(i, count, isHero: true, labelKey: "EndOfGameMultipleInjuryLabel");
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            MultipleInjuryRolls.Add(entry);
        }

        OnPropertyChanged(nameof(HasMultipleInjuryRolls));
    }

    /// <summary>Ajuste FigureInjuryRolls pour qu'il compte exactement un jet D6 par figurine hors de
    /// combat (OutOfActionCount), en préservant les jets déjà faits quand le nombre ne diminue pas -
    /// même principe que SyncAdvanceRolls/PopulateMultipleInjuryRolls, mais additif comme SyncAdvanceRolls
    /// (pas de reset complet) puisque le joueur peut ajuster le stepper dans un sens ou dans l'autre
    /// avant de lancer les dés. Total est mis à jour sur les entrées déjà là (label "Figurine i/N") au
    /// lieu d'être figé à leur création, contrairement à AdvanceRollEntry/InjurySubRollEntry des
    /// Blessures multiples dont le total ne varie jamais après coup.</summary>
    private void SyncFigureInjuryRolls()
    {
        while (FigureInjuryRolls.Count < OutOfActionCount)
        {
            var entry = new InjurySubRollEntry(FigureInjuryRolls.Count + 1, OutOfActionCount, isHero: false, labelKey: "EndOfGameFigureLabel");
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            FigureInjuryRolls.Add(entry);
        }
        while (FigureInjuryRolls.Count > OutOfActionCount)
            FigureInjuryRolls.RemoveAt(FigureInjuryRolls.Count - 1);

        foreach (var entry in FigureInjuryRolls)
            entry.UpdateTotal(OutOfActionCount);
    }

    /// <summary>Ajuste AdvanceRolls pour qu'il compte exactement un AdvanceRollEntry par palier
    /// franchi (MilestoneCount), en préservant les jets déjà faits quand le nombre ne diminue pas -
    /// appelé à chaque frappe dans le champ PX de l'étape Expérience (OnExperienceGainedChanged).</summary>
    private void SyncAdvanceRolls()
    {
        while (AdvanceRolls.Count < MilestoneCount)
        {
            var entry = new AdvanceRollEntry(AdvanceRolls.Count + 1, Warrior.IsHero);
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            AdvanceRolls.Add(entry);
        }
        while (AdvanceRolls.Count > MilestoneCount)
            AdvanceRolls.RemoveAt(AdvanceRolls.Count - 1);
    }
}

/// <summary>One 2D6 progression roll for one milestone crossed by a WarriorOutcomeRow - see
/// WarriorOutcomeRow.AdvanceRolls/SyncAdvanceRolls (a warrior can cross several milestones in the
/// same End of Game, each needing its own independent roll).</summary>
public partial class AdvanceRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public int Index { get; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc["EndOfGameMilestoneLabel"], Index);

    /// <summary>Le score 2D6 - saisi à la main (jet physique) ou rempli par AutoRollAdvance. Dès que la
    /// valeur est un jet complet et valide, ResultText se résout tout seul (OnManualRollChanged).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkillResult))]
    private string manualRoll = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.RollError, posé uniquement par
    /// EndOfGameDialogViewModel.Next si ce jet est encore vide/invalide à ce moment-là.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        ResultText = string.Empty;
        if (!int.TryParse(value, out var roll)) return;

        bool found;
        string key;
        found = IsHero ? HeroAdvanceTable.TryGetTextKey(roll, out key) : HenchmanAdvanceTable.TryGetTextKey(roll, out key);
        if (found)
        {
            ResultText = _loc[key];
            RollError = null;
        }
    }

    /// <summary>Texte descriptif du résultat une fois résolu - purement informatif, voir
    /// HeroAdvanceTable/HenchmanAdvanceTable.</summary>
    [ObservableProperty]
    private string resultText = string.Empty;

    /// <summary>Seuls les résultats "Compétence" des Héros (voir HeroAdvanceTable.IsSkill) proposent
    /// de choisir directement une compétence - les résultats de stat/choix (6/7/8/9) et la
    /// promotion Homme de main (10-12, "Ce gars est doué") restent du texte descriptif.</summary>
    public bool IsSkillResult => IsHero && int.TryParse(ManualRoll, out var roll) && HeroAdvanceTable.IsSkill(roll);

    /// <summary>Compétence(s) choisie(s) pour ce jet - rattachée(s) au guerrier par
    /// WarbandDetailViewModel.EndOfGame à l'enregistrement, voir PickAdvanceSkill.</summary>
    public ObservableCollection<Skill> SelectedSkills { get; } = new();
    public string SelectedSkillsText => string.Join(", ", SelectedSkills.Select(s => s.Name));

    /// <summary>Pilote l'affichage exclusif bouton "Choisir une compétence" / nom(s) choisi(s) dans le
    /// XAML - une fois une compétence sélectionnée, son nom remplace le bouton plutôt que de
    /// s'afficher à côté.</summary>
    public bool HasSkillSelected => SelectedSkills.Count > 0;

    public AdvanceRollEntry(int index, bool isHero)
    {
        Index = index;
        IsHero = isHero;
        SelectedSkills.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedSkillsText));
            OnPropertyChanged(nameof(HasSkillSelected));
        };
    }
}

/// <summary>One injury roll stacked under a WarriorOutcomeRow, in one of two unrelated situations that
/// happen to share the exact same shape (an indexed D66/D6 roll auto-resolving to text): (1) a Hero's
/// D66 sub-roll from a "Blessures multiples" result (16/21) - see
/// WarriorOutcomeRow.MultipleInjuryRolls/PopulateMultipleInjuryRolls; (2) one D6 roll per Henchman
/// group model marked out of action - see WarriorOutcomeRow.FigureInjuryRolls/SyncFigureInjuryRolls.
/// IsHero picks which table resolves ManualRoll (always Hero/D66 for case 1, always Henchman/D6 for
/// case 2 - never mixed within one collection). Same "accept the result as-is" stance as the main
/// injury roll for a sub-roll landing on Dead/Captured/Multiple Injuries again: the rulebook says to
/// re-roll but the app leaves that to the player rather than enforcing it (see SeriousInjuryTable's doc
/// comment).</summary>
public partial class InjurySubRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly string _labelKey;

    public int Index { get; }
    public int Total { get; set; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc[_labelKey], Index, Total);

    [ObservableProperty]
    private string manualRoll = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.RollError, posé uniquement par
    /// EndOfGameDialogViewModel.Next si ce jet est encore vide/invalide à ce moment-là.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        InjuryResultText = string.Empty;
        if (!int.TryParse(value, out var roll)) return;

        bool found;
        string key;
        found = IsHero ? SeriousInjuryTable.TryGetTextKey(roll, out key) : HenchmanInjuryTable.TryGetTextKey(roll, out key);
        if (found)
        {
            InjuryResultText = _loc[key];
            RollError = null;
        }
    }

    [ObservableProperty]
    private string injuryResultText = string.Empty;

    /// <summary>True si le jet actuellement saisi est un résultat de mort (Héros 11-15, Homme de main
    /// 1-2) - utilisé par WarbandDetailViewModel.EndOfGame pour compter les figurines perdues dans un
    /// groupe d'Hommes de main (voir WarriorOutcomeRow.FigureInjuryRolls). Sans objet pour les sous-jets
    /// de Blessures multiples d'un Héros (déjà géré au niveau du jet principal, voir ApplyInjuryRoll).</summary>
    public bool IsDeath => int.TryParse(ManualRoll, out var roll) && (IsHero ? SeriousInjuryTable.IsDeath(roll) : HenchmanInjuryTable.IsDeath(roll));

    public InjurySubRollEntry(int index, int total, bool isHero, string labelKey)
    {
        Index = index;
        Total = total;
        IsHero = isHero;
        _labelKey = labelKey;
    }

    /// <summary>Steps.SyncFigureInjuryRolls-style syncs (voir WarriorOutcomeRow.SyncFigureInjuryRolls)
    /// n'ajoutent/ne retirent qu'en bout de liste et préservent les entrées existantes - mais Total (le
    /// nombre total affiché dans Label, ex. "Figurine 2/3") doit rester à jour sur celles-ci quand le
    /// compte global change.</summary>
    public void UpdateTotal(int total)
    {
        if (Total == total) return;
        Total = total;
        OnPropertyChanged(nameof(Label));
    }
}

/// <summary>One D6 of the Exploration roll (see EndOfGameDialogViewModel.ExplorationDice/
/// SyncExplorationDice) - as many entries as ExplorationDiceCount computes, each independently
/// filled by hand or by AutoRollExplorationDie. Value is null while empty/invalid (1-6 only), same
/// "string Entry, not int" idiom as every other roll field in this wizard.</summary>
public partial class ExplorationDieEntry : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    private string manualRoll = string.Empty;

    [ObservableProperty]
    private string? rollError;

    public int? Value => int.TryParse(ManualRoll, out var v) && v is >= 1 and <= 6 ? v : null;

    partial void OnManualRollChanged(string value)
    {
        OnPropertyChanged(nameof(Value));
        if (Value is not null) RollError = null;
    }

    public ExplorationDieEntry(int index) => Index = index;
}
