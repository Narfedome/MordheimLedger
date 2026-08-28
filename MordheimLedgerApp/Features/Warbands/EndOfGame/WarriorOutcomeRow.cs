using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

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

    /// <summary>Copie de WarriorRow.MagicSchools (bande entière, déjà vide côté appelant pour tout
    /// guerrier dont l'archétype n'est pas IsSpellcaster - voir WarriorRow) - consommée par
    /// EndOfGameDialogViewModel.PickAdvanceSpell (tirage 1D6 sur ces écoles, même mécanisme que
    /// WarriorEditDialogViewModel.AddSpell). IsSpellcaster (passé à chaque AdvanceRollEntry créé par ce
    /// row, pour ShowSpellOption) en est directement dérivé.</summary>
    public IReadOnlyList<MagicSchool> MagicSchools { get; }

    public bool IsSpellcaster => MagicSchools.Count > 0;

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
    [NotifyPropertyChangedFor(nameof(ExplorationMilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasExplorationMilestone))]
    private string experienceGainedText = string.Empty;

    partial void OnExperienceGainedTextChanged(string value)
    {
        SyncAdvanceRolls();
        // ExplorationMilestoneCount's starting point (Warrior.Experience + ExperienceGained) shifts too -
        // only reachable in practice if the player backs up to the Experience step after already having
        // assigned Exploration XP, but kept in sync regardless rather than left stale.
        SyncExplorationAdvanceRolls();
    }

    public int ExperienceGained => int.TryParse(ExperienceGainedText, out var xp) ? xp : 0;

    /// <summary>Points d'Expérience alloués à ce Héros via le steppeur de répartition (Prisonniers,
    /// Possédés, Cimetière - voir ExplorationOutcome.GrantsDistributedHeroExperienceFormula) -
    /// totalement distinct d'ExperienceGained (l'étape Expérience, plus tôt dans le wizard, pour l'XP de
    /// bataille normale) : deux sources d'XP différentes, jamais mélangées pour éviter qu'un retour en
    /// arrière sur l'étape Expérience affiche une valeur modifiée par une étape plus tardive. Remis à 0 à
    /// chaque nouveau résultat d'Exploration déclenché (EndOfGameDialogViewModel.ResolveExplorationResult),
    /// pas seulement quand ce guerrier change.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExplorationBonusExperience))]
    [NotifyPropertyChangedFor(nameof(ExplorationMilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasExplorationMilestone))]
    private int distributedExplorationExperience;

    partial void OnDistributedExplorationExperienceChanged(int value) => SyncExplorationAdvanceRolls();

    /// <summary>Montant fixe accordé par une branche d'Exploration ciblant TOUJOURS le chef, sans jet ni
    /// choix du joueur (Traînard, branche Possédés - "le chef gagne +1 Expérience", voir
    /// ExplorationOutcome.GrantsLeaderExperience) - renseigné par EndOfGameDialogViewModel sur la seule
    /// ligne dont Warrior.IsLeader est vrai quand cette branche se résout (0 pour tout autre guerrier),
    /// même idiome que DistributedExplorationExperience mais sans steppeur puisqu'il n'y a rien à
    /// répartir.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExplorationBonusExperience))]
    [NotifyPropertyChangedFor(nameof(ExplorationMilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasExplorationMilestone))]
    private int leaderExplorationExperience;

    partial void OnLeaderExplorationExperienceChanged(int value) => SyncExplorationAdvanceRolls();

    /// <summary>Total de l'XP accordée par l'Exploration pour ce guerrier, toutes sources confondues
    /// (répartition Héros + chef fixe - jamais les deux en même temps en pratique aujourd'hui, mais rien
    /// n'empêche qu'un futur résultat les cumule).</summary>
    public int ExplorationBonusExperience => DistributedExplorationExperience + LeaderExplorationExperience;

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
    [NotifyPropertyChangedFor(nameof(ShowHatredSection))]
    [NotifyPropertyChangedFor(nameof(ShowInjuryBranchSubRoll))]
    [NotifyPropertyChangedFor(nameof(InjuryBranchSpecialRules))]
    [NotifyPropertyChangedFor(nameof(HasInjuryBranchSpecialRules))]
    [NotifyPropertyChangedFor(nameof(ShowDeepWoundSubRoll))]
    [NotifyPropertyChangedFor(nameof(ShowCapturedChoice))]
    [NotifyPropertyChangedFor(nameof(ShowSoldToThePits))]
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
        if (int.TryParse(value, out var roll))
            ResolveInjuryResult(roll);
        else
            // Jet effacé/invalide (ex. retiré après un résultat de Mort) - repasse Actif plutôt que de
            // laisser le marquage Mort précédent affiché sans jet qui le justifie (bug corrigé
            // 2026-08-25, voir ApplyInjuryRoll).
            SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == WarriorStatus.Active).Key;
        if (!string.IsNullOrWhiteSpace(InjuryResultText)) RollError = null;

        // Si le jet principal ne donne plus "Rancune", le sous-jet/la cible précédemment saisis n'ont
        // plus de sens - effacés plutôt que laissés affichés pour un résultat qui ne les déclenche plus.
        if (Warrior.IsHero && !ShowHatredSection && HatredSubRoll.Length > 0)
            HatredSubRoll = string.Empty;

        // Même principe pour le sous-jet de branche (Blessure au bras/Jambe écrasée, 23/25) - un
        // nouveau jet principal qui ne tombe plus sur l'un de ces deux résultats invalide le sous-jet
        // déjà saisi.
        if (Warrior.IsHero && !ShowInjuryBranchSubRoll && InjuryBranchSubRoll.Length > 0)
            InjuryBranchSubRoll = string.Empty;

        // Même principe pour le sous-jet de Blessure profonde (35) - un nouveau jet principal qui ne
        // tombe plus sur ce résultat invalide le sous-jet déjà saisi.
        if (Warrior.IsHero && !ShowDeepWoundSubRoll && DeepWoundSubRoll.Length > 0)
            DeepWoundSubRoll = string.Empty;

        // Même principe pour le choix de Capturé (61) - un nouveau jet principal qui ne tombe plus sur
        // ce résultat invalide le choix déjà fait.
        if (Warrior.IsHero && !ShowCapturedChoice && IsRansomed)
            IsRansomed = false;

        // Vendu aux Fosses (65) : le sous-jet de relance (défaite) est affiché dès l'entrée dans ce
        // résultat, WonPitFight partant décoché par défaut - même principe d'auto-peuplement que
        // PopulateMultipleInjuryRolls, mais sans jet intermédiaire ("combien de sous-jets") puisqu'il n'y
        // en a toujours qu'un seul ici.
        if (Warrior.IsHero && ShowSoldToThePits && !WonPitFight && SoldToPitsRerollRoll.Count == 0)
            PopulateSoldToPitsRerollRoll();

        // Même principe pour Vendu aux Fosses (65) - un nouveau jet principal qui ne tombe plus sur ce
        // résultat invalide le choix victoire/défaite et le sous-jet de relance déjà saisis.
        if (Warrior.IsHero && !ShowSoldToThePits && WonPitFight)
            WonPitFight = false;
        if (Warrior.IsHero && !ShowSoldToThePits && SoldToPitsRerollRoll.Count > 0)
        {
            SoldToPitsRerollRoll.Clear();
            OnPropertyChanged(nameof(HasSoldToPitsRerollRoll));
        }

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
        // Toujours synchronisé, même si le jet ne correspond à aucune entrée valide de la table
        // (ex. "17") - IsDeath(roll) est alors simplement false, ce qui repasse correctement Actif au
        // lieu de laisser un marquage Mort précédent affiché sans jet qui le justifie.
        ApplyInjuryRoll(roll);

        bool found;
        string key;
        found = Warrior.IsHero ? SeriousInjuryTable.TryGetTextKey(roll, out key) : HenchmanInjuryTable.TryGetTextKey(roll, out key);
        if (!found) return;

        InjuryResultText = _loc[key];
    }

    /// <summary>Texte complet du résultat une fois consulté (via AutoRoll ou ShowInjuryResult) -
    /// c'est ce texte qui alimente la note du guerrier et la phrase d'Historique à la sauvegarde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ResolvedInjuryText))]
    private string injuryResultText = string.Empty;

    /// <summary>Un jet 2D6 par palier d'XP franchi cette partie (voir SyncAdvanceRolls) - un guerrier
    /// qui saute plusieurs cases à bord épais d'un coup doit relancer autant de fois sur la table de
    /// progression (Campagne.md § Expérience). Distincte du jet de Blessure Grave (ManualRoll) : les
    /// deux peuvent coexister le même End of Game.</summary>
    public ObservableCollection<AdvanceRollEntry> AdvanceRolls { get; } = new();

    /// <summary>Même mécanique qu'AdvanceRolls (mêmes AdvanceRollEntry/tables de jet), pour les paliers
    /// franchis uniquement par ExplorationBonusExperience - voir ExplorationMilestoneCount pour pourquoi
    /// c'est une deuxième collection plutôt que la même, synchronisée par SyncExplorationAdvanceRolls.</summary>
    public ObservableCollection<AdvanceRollEntry> ExplorationAdvanceRolls { get; } = new();

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

    /// <summary>True dès que le jet principal (ManualRoll) donne "Rancune" (56, Héros uniquement) -
    /// pilote l'affichage du bloc "qui hait-il" dans le XAML : le sous-jet 1D6 d'abord (portée), puis un
    /// champ adapté à cette portée. L'appli ne suit pas les bandes/guerriers adverses comme données
    /// structurées (retour utilisateur explicite : rien à choisir dans un picker de toute façon) - seule
    /// la portée "toutes les bandes de ce type" (6) référence un vrai WarbandArchetype du catalogue ;
    /// les 3 autres portées sont un simple nom tapé au clavier (voir HatredTargetFreeTextInput).</summary>
    public bool ShowHatredSection => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && SeriousInjuryTable.IsBitterEnmity(roll);

    /// <summary>Le score du 1D6 tiré pour savoir quelle est la portée de la Haine (voir Core.Rules.
    /// HatredTargetTable) - saisi à la main ou rempli par AutoRollHatred, même convention que les autres
    /// jets de cette étape.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(HatredScope))]
    [NotifyPropertyChangedFor(nameof(ShowHatredFreeTextEntry))]
    [NotifyPropertyChangedFor(nameof(ShowHatredArchetypePicker))]
    [NotifyPropertyChangedFor(nameof(HatredFreeTextPlaceholder))]
    private string hatredSubRoll = string.Empty;

    /// <summary>Résolu depuis HatredSubRoll (voir Core.Rules.HatredTargetTable) - null tant que le
    /// sous-jet n'est pas encore saisi/valide. Nommé "Scope" plutôt que "Kind" pour ne pas entrer en
    /// collision avec le nom du type énuméré Core.Rules.HatredTargetKind lui-même (même précédent que
    /// Warrior Warrior dans ce fichier, mais un enum ne peut pas être référencé par membre statique
    /// (HatredTargetKind.SpecificWarrior) une fois son propre nom masqué par une propriété - contrairement
    /// à une classe, l'accès qualifié via l'espace de noms n'aide pas ici).</summary>
    public HatredTargetKind? HatredScope =>
        int.TryParse(HatredSubRoll, out var roll) && HatredTargetTable.TryGetOutcome(roll, out var kind) ? kind : null;

    /// <summary>Portées "individu" (1-4) et "cette bande" (5) : un simple champ texte, pas de picker -
    /// l'app ne suit pas les guerriers/bandes adverses. Placeholder différent selon la portée (voir
    /// HatredFreeTextPlaceholder), même mécanisme de saisie sinon.</summary>
    public bool ShowHatredFreeTextEntry => HatredScope is HatredTargetKind.SpecificWarrior or HatredTargetKind.SpecificWarband;

    public string HatredFreeTextPlaceholder => HatredScope == HatredTargetKind.SpecificWarband
        ? _loc["EndOfGameHatredBandNamePh"]
        : _loc["EndOfGameHatredIndividualNamePh"];

    /// <summary>Saisie libre du nom (individu/chef en portée 1-4, bande en portée 5) - se résout tout
    /// seul à la frappe (OnHatredTargetFreeTextInputChanged), même convention que le reste de ce wizard.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(HasHatredTarget))]
    private string hatredTargetFreeTextInput = string.Empty;

    partial void OnHatredTargetFreeTextInputChanged(string value)
    {
        HatredTargetDisplayName = value;
        HatredTargetFreeText = string.IsNullOrWhiteSpace(value) ? null : value;
        if (!string.IsNullOrWhiteSpace(value)) HatredRollError = null;
    }

    /// <summary>Portée "toutes les bandes de ce type" (6) : seule portée référençant un vrai catalogue
    /// (WarbandArchetype), donc le seul cas gardant un picker structuré (PickHatredWarbandArchetype).</summary>
    public bool ShowHatredArchetypePicker => HatredScope == HatredTargetKind.WarbandArchetype;

    partial void OnHatredSubRollChanged(string value)
    {
        // Un nouveau sous-jet invalide la cible précédemment résolue - même principe que
        // OnManualRollChanged pour les sous-jets Blessures multiples.
        HatredTargetWarbandArchetype = null;
        HatredTargetWarbandArchetypeId = null;
        HatredTargetFreeText = null;
        HatredTargetFreeTextInput = string.Empty;
        HatredTargetDisplayName = string.Empty;
        HatredRollError = null;
    }

    public int? HatredTargetWarbandArchetypeId { get; private set; }
    public string? HatredTargetFreeText { get; private set; }

    /// <summary>Nom résolu de la cible finale - vide tant qu'elle n'est pas résolue (à la frappe pour
    /// une portée en texte libre, via PickHatredWarbandArchetypeCommand pour la portée 6). Pas de préfixe
    /// "Haine :" ici (voir WarriorHatred.Name) - purement informatif pour ce wizard, le préfixe
    /// s'applique à l'affichage en chip une fois la partie enregistrée (WarbandDetailViewModel.
    /// BuildSpecialRuleChips's cousin, voir WarbandDetailViewModel.EndOfGame.ApplyWarriorOutcomesAsync).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(HasHatredTarget))]
    private string hatredTargetDisplayName = string.Empty;

    public bool HasHatredTarget => HatredTargetDisplayName.Length > 0;

    /// <summary>Même principe que RollError/MultipleInjuryCountError, pour le sous-jet de Rancune -
    /// "jet requis" tant que HatredScope est null, "cible requise" si la portée 6 attend encore
    /// PickHatredWarbandArchetype (voir ValidateInjuryStep).</summary>
    [ObservableProperty]
    private string? hatredRollError;

    /// <summary>L'archétype de bande résolu (portée 6 uniquement) - conservé en plus de
    /// HatredTargetWarbandArchetypeId/HatredTargetDisplayName pour que la chip de confirmation
    /// (ChipView) puisse s'y lier directement, tap-to-detail et retrait compris, même langage que le
    /// reste de l'app plutôt qu'un label brut ("comme toujours une chip", retour utilisateur explicite).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(HasHatredTarget))]
    private WarbandArchetype? hatredTargetWarbandArchetype;

    /// <summary>Appelée par EndOfGameDialogViewModel.Injury.PickHatredWarbandArchetype une fois le dialog
    /// résolu (portée 6 uniquement - les autres se résolvent à la frappe, voir
    /// OnHatredTargetFreeTextInputChanged).</summary>
    public void SetHatredTarget(WarbandArchetype archetype)
    {
        HatredTargetWarbandArchetype = archetype;
        HatredTargetWarbandArchetypeId = archetype.Id;
        HatredTargetFreeText = null;
        HatredTargetDisplayName = archetype.Name;
        HatredRollError = null;
    }

    /// <summary>Croix de la chip (portée 6) - remet la portée à "non résolue" sans effacer le sous-jet,
    /// pour que le joueur puisse re-choisir un autre type de bande sans tout refaire.</summary>
    public void ClearHatredTargetWarbandArchetype()
    {
        HatredTargetWarbandArchetype = null;
        HatredTargetWarbandArchetypeId = null;
        HatredTargetDisplayName = string.Empty;
    }

    /// <summary>True dès que le jet principal (ManualRoll) donne "Blessure au bras" (23), "Jambe
    /// écrasée" (25) ou "Folie" (24, Héros uniquement) - les trois résultats dont la résolution finale
    /// dépend d'un sous-jet 1D6 (voir Core.Rules.SeriousInjuryEffectTable.RequiresBranchSubRoll). Le
    /// texte des deux branches est déjà visible dans InjuryResultText (résolu dès le jet principal) -
    /// ce sous-jet sert uniquement à déterminer laquelle s'applique réellement (un effet mécanisé pour
    /// 23/25, un rappel de règle - SpecialRule attachée à l'Injury - pour 24, voir
    /// Injury.SpecialRules).</summary>
    public bool ShowInjuryBranchSubRoll => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && SeriousInjuryEffectTable.RequiresBranchSubRoll(roll);

    /// <summary>Le score du 1D6 tiré pour cette branche - saisi à la main ou rempli par
    /// AutoRollInjuryBranch, même convention que ManualRoll/HatredSubRoll.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(InjuryBranchResultText))]
    [NotifyPropertyChangedFor(nameof(ResolvedInjuryText))]
    [NotifyPropertyChangedFor(nameof(InjuryBranchSpecialRules))]
    [NotifyPropertyChangedFor(nameof(HasInjuryBranchSpecialRules))]
    private string injuryBranchSubRoll = string.Empty;

    partial void OnInjuryBranchSubRollChanged(string value)
    {
        if (HasValidInjuryBranchSubRoll) InjuryBranchRollError = null;
    }

    /// <summary>Texte propre à la branche réellement résolue (voir Core.Rules.SeriousInjuryTable.
    /// TryGetBranchTextKey) - "Blessure au bras : amputé"/"... : légère" plutôt que le texte du livre qui
    /// décrit les deux branches à la fois (déjà affiché au-dessus via InjuryResultText, comme contexte
    /// avant le sous-jet). Vide tant que le sous-jet n'est pas encore saisi/valide.</summary>
    public string InjuryBranchResultText =>
        int.TryParse(ManualRoll, out var roll) && int.TryParse(InjuryBranchSubRoll, out var subRoll) &&
        SeriousInjuryTable.TryGetBranchTextKey(roll, subRoll, out var key) ? _loc[key] : string.Empty;

    /// <summary>Le texte à utiliser partout où "le résultat de Blessure de ce guerrier" est affiché/
    /// enregistré (Récapitulatif, chip catalogue de repli, phrase d'Historique) - la branche résolue
    /// (23/25) une fois connue, sinon le texte général déjà résolu par le jet principal.</summary>
    public string ResolvedInjuryText => InjuryBranchResultText.Length > 0 ? InjuryBranchResultText : InjuryResultText;

    /// <summary>Règle(s) spéciale(s) que la branche résolue accorde de façon permanente (Folie 24 ->
    /// Stupidité/Frénésie, voir Injury.SpecialRules) - prévisualisée en direct dans le wizard via
    /// InjuryCatalogLookup, la même résolution par jet que WarbandDetailViewModel.EndOfGame.
    /// GetOrCreateInjuryAsync fera à l'enregistrement. Vide pour 23/25 (aucune SpecialRule attachée à
    /// ces branches) et tant que le sous-jet n'est pas encore saisi/valide.</summary>
    public IReadOnlyList<SpecialRule> InjuryBranchSpecialRules
    {
        get
        {
            if (!int.TryParse(ManualRoll, out var roll) || !int.TryParse(InjuryBranchSubRoll, out var subRoll))
                return Array.Empty<SpecialRule>();

            var category = Warrior.IsHero ? InjuryCategory.Hero : InjuryCategory.Henchman;
            return (IReadOnlyList<SpecialRule>?)InjuryCatalogLookup.Find(_injuryCatalog, category, roll, subRoll)?.SpecialRules ?? Array.Empty<SpecialRule>();
        }
    }

    public bool HasInjuryBranchSpecialRules => InjuryBranchSpecialRules.Count > 0;

    /// <summary>Résolu depuis ManualRoll+InjuryBranchSubRoll (voir Core.Rules.SeriousInjuryEffectTable.
    /// TryGetBranchSubRollOutcome) - null tant que le sous-jet n'est pas encore saisi/valide, ainsi que
    /// pour la branche grave (1) qui reste hors Palier 1 (aucun effet mécanisé, cf. la doc de la
    /// table). Consommé par WarbandDetailViewModel.EndOfGame.ApplyWarriorOutcomesAsync pour appliquer
    /// l'effet réel.</summary>
    public SeriousInjuryOutcome? InjuryBranchOutcome =>
        int.TryParse(ManualRoll, out var roll) && int.TryParse(InjuryBranchSubRoll, out var subRoll) &&
        SeriousInjuryEffectTable.TryGetBranchSubRollOutcome(roll, subRoll, out var outcome) ? outcome : null;

    /// <summary>True dès que le sous-jet est un 1D6 valide (1-6), y compris la branche 1 (grave) qui ne
    /// produit pas d'InjuryBranchOutcome (Palier 2, aucun effet mécanisé) - contrairement à
    /// InjuryBranchOutcome, sert uniquement à valider que le joueur a bien saisi un jet avant de
    /// continuer (ValidateInjuryStep).</summary>
    public bool HasValidInjuryBranchSubRoll => int.TryParse(InjuryBranchSubRoll, out var subRoll) && subRoll is >= 1 and <= 6;

    /// <summary>Même principe que RollError/HatredRollError, pour le sous-jet de branche.</summary>
    [ObservableProperty]
    private string? injuryBranchRollError;

    /// <summary>True dès que le jet principal (ManualRoll) donne "Blessure profonde" (35, Héros
    /// uniquement) - seul résultat Palier 1 dont l'effet (nombre de parties manquées) dépend d'un
    /// sous-jet 1D3 plutôt que d'être fixe. Auparavant tiré silencieusement par l'appli
    /// (SeriousInjuryEffectTable.RollD3()) sans que le joueur ne le voie ni ne le saisisse - retour
    /// utilisateur (2026-08-26) : "il nous faut le tirage du nombre de partie manquée (avec un
    /// indicateur du nombre d'indisponibilité)". Même idiome de saisie manuelle + bouton dé optionnel
    /// que ShowInjuryBranchSubRoll.</summary>
    public bool ShowDeepWoundSubRoll => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && roll == 35;

    /// <summary>Le score du 1D3 tiré (1 à 3) - saisi à la main ou rempli par AutoRollDeepWound, même
    /// convention que InjuryBranchSubRoll.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(DeepWoundConfirmationText))]
    private string deepWoundSubRoll = string.Empty;

    partial void OnDeepWoundSubRollChanged(string value)
    {
        if (HasValidDeepWoundSubRoll) DeepWoundRollError = null;
    }

    /// <summary>True dès que le sous-jet est un 1D3 valide (1-3) - sert à la fois à valider avant de
    /// continuer (ValidateInjuryStep) et à résoudre le nombre réel de parties manquées appliqué à la
    /// sauvegarde (voir WarbandDetailViewModel.EndOfGame.ApplyWarriorOutcomesAsync).</summary>
    public bool HasValidDeepWoundSubRoll => int.TryParse(DeepWoundSubRoll, out var subRoll) && subRoll is >= 1 and <= 3;

    /// <summary>Confirmation affichée sous le sous-jet une fois valide, ex. "2 parties manquées" -
    /// l'indicateur demandé par l'utilisateur, distinct du texte de référence statique
    /// (InjuryResultText, qui ne mentionne que "1D3" en toutes lettres).</summary>
    public string DeepWoundConfirmationText =>
        HasValidDeepWoundSubRoll && int.TryParse(DeepWoundSubRoll, out var subRoll)
            ? string.Format(_loc["EndOfGameDeepWoundResultFormat"], subRoll)
            : string.Empty;

    /// <summary>Même principe que RollError/InjuryBranchRollError, pour le sous-jet de Blessure
    /// profonde.</summary>
    [ObservableProperty]
    private string? deepWoundRollError;

    /// <summary>True dès que le jet principal (ManualRoll) donne "Capturé" (61, Héros uniquement).
    /// Portée revue à la baisse (2026-08-27) : le livre décrit 5 issues nommées, mais toutes racontent
    /// une décision du CAPTEUR (une bande adverse) - l'appli ne modélise pas encore l'autre bande comme
    /// donnée structurée (voir la note mémoire sur un futur système en réseau/lobby). En attendant,
    /// seule la distinction qui affecte réellement CE guerrier compte : racheté contre rançon (revient,
    /// coût déduit de notre trésorerie) ou perdu (considéré mort, comme toute autre issue du livre -
    /// échangé/vendu/tué/sacrifié se valent toutes de notre point de vue, aucune ne nous revient).</summary>
    public bool ShowCapturedChoice => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && roll == 61;

    /// <summary>Coché si le joueur choisit de payer une rançon pour récupérer ce guerrier - décoché
    /// (par défaut) signifie "perdu" (voir Core.Rules... non, pas de table Core ici, la logique est
    /// trop simple pour le justifier : voir WarbandDetailViewModel.EndOfGame.ApplyWarriorOutcomesAsync).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private bool isRansomed;

    partial void OnIsRansomedChanged(bool value)
    {
        if (!value) RansomAmount = string.Empty;
    }

    /// <summary>Montant de la rançon en CO, saisi par le joueur (négocié entre les deux joueurs à la
    /// table, aucune formule dans le livre) - déduit de Warband.Treasury à l'enregistrement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string ransomAmount = string.Empty;

    public bool HasValidRansomAmount => int.TryParse(RansomAmount, out var amount) && amount >= 0;

    partial void OnRansomAmountChanged(string value)
    {
        if (HasValidRansomAmount) CapturedChoiceError = null;
    }

    /// <summary>Même principe que RollError/InjuryBranchRollError, pour le montant de la rançon.</summary>
    [ObservableProperty]
    private string? capturedChoiceError;

    /// <summary>True dès que le jet principal (ManualRoll) donne "Vendu aux Fosses" (65, Héros
    /// uniquement) - combat de gladiateur contre un Gladiateur ("Pit Fighter", catalogue Franc-Tireur/
    /// HiredSword - voir Models.Library.HiredSword). Ce Gladiateur est un adversaire éphémère affiché
    /// pour comparaison de profil, jamais recruté dans notre bande (voir
    /// HiredSwordEditDialogViewModel/HiredSwordViewModel - aucun flux d'engagement réel dans cette
    /// passe). Comme pour Capturé, aucun moteur de combat : le joueur résout l'affrontement lui-même
    /// (à la table ou en tête) et coche simplement victoire/défaite ci-dessous.</summary>
    public bool ShowSoldToThePits => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && roll == 65;

    /// <summary>Profil du Gladiateur ("Pit Fighter", catalogue Franc-Tireur/HiredSword) résolu une seule
    /// fois à l'ouverture du wizard (voir WarbandDetailViewModel.EndOfGame.EndOfGame) et transmis à
    /// chaque ligne - null si cette entrée du catalogue n'a jamais été seedée (ne devrait pas arriver en
    /// pratique). Affiché en StatRowView à côté du profil de ce guerrier pour la comparaison de la
    /// fiche officielle.</summary>
    public HiredSword? PitFighterProfile { get; }

    /// <summary>Équipement de départ du Gladiateur, déjà résolu en vrais EquipmentItem (voir
    /// HiredSword.StartingEquipmentIds) - même résolution une seule fois à l'ouverture du wizard que
    /// PitFighterProfile.</summary>
    public IReadOnlyList<EquipmentItem> PitFighterEquipment { get; }

    /// <summary>Compétences autorisées du Gladiateur en toutes lettres (ex. "Combat, Vitesse, Force") -
    /// jamais de compétence réellement APPRISE à afficher (jamais recruté, voir PitFighterProfile), donc
    /// un simple texte plutôt qu'une ChipListView de WarriorSkill comme pour ce guerrier.</summary>
    public string PitFighterAllowedSkillCategoriesText => PitFighterProfile is null
        ? string.Empty
        : string.Join(", ", PitFighterProfile.AllowedSkillCategories.Select(c => _loc[$"SkillCategory{c}"]));

    /// <summary>Pilote l'affichage de la section Équipement de la "fiche perso" comparative de Vendu aux
    /// Fosses - même idiome que WarriorRow.HasEquipment côté roster.</summary>
    public bool HasEquipment => Warrior.Equipment.Count > 0;

    /// <summary>Idem pour Compétences - même idiome que WarriorRow.HasSkills.</summary>
    public bool HasSkills => Warrior.Skills.Count > 0;

    /// <summary>Idem côté carte du Gladiateur, pour son équipement de départ fixe (PitFighterEquipment).</summary>
    public bool HasPitFighterEquipment => PitFighterEquipment.Count > 0;

    /// <summary>Coché si le joueur gagne le combat de gladiateur (+50 CO, +2 PX, garde son équipement) -
    /// décoché (par défaut) signifie défaite (un sous-jet supplémentaire, voir SoldToPitsRerollRoll).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private bool wonPitFight;

    partial void OnWonPitFightChanged(bool value)
    {
        if (value && SoldToPitsRerollRoll.Count > 0)
        {
            SoldToPitsRerollRoll.Clear();
            OnPropertyChanged(nameof(HasSoldToPitsRerollRoll));
        }
        else if (!value && ShowSoldToThePits && SoldToPitsRerollRoll.Count == 0)
        {
            PopulateSoldToPitsRerollRoll();
        }
    }

    /// <summary>Défaite du combat : un unique sous-jet D66 (relance sur la même table de Blessures
    /// Graves) - réutilise InjurySubRollEntry tel quel (même patron que MultipleInjuryRolls) plutôt qu'un
    /// nouveau type, il résout déjà un jet complet avec son propre IsDeath. Le livre dit de ne garder que
    /// 11-35 ("reroll keeping only 11-35") - une consigne purement indicative (Label d'aide dans le XAML),
    /// jamais validée : aucun jet n'est bloqué ailleurs dans l'appli, celui-ci ne fait pas exception.
    /// Collection à un seul élément (plutôt qu'un champ nu) pour réutiliser le même DataTemplate/les mêmes
    /// commandes AutoRoll que MultipleInjuryRolls/FigureInjuryRolls dans le XAML.</summary>
    public ObservableCollection<InjurySubRollEntry> SoldToPitsRerollRoll { get; } = new();
    public bool HasSoldToPitsRerollRoll => SoldToPitsRerollRoll.Count > 0;

    private void PopulateSoldToPitsRerollRoll()
    {
        var entry = new InjurySubRollEntry(1, 1, isHero: true, labelKey: "EndOfGameSoldToPitsRerollLabel");
        entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
        SoldToPitsRerollRoll.Add(entry);
        OnPropertyChanged(nameof(HasSoldToPitsRerollRoll));
    }

    /// <summary>Un jet D6 par figurine hors de combat dans un groupe d'Hommes de main (OutOfActionCount)
    /// - sans objet pour un Héros, qui utilise ManualRoll/InjuryResultText ci-dessus à la place (une
    /// seule figurine, un seul jet). Peuplée/resynchronisée par SyncFigureInjuryRolls à chaque
    /// changement d'OutOfActionCount.</summary>
    public ObservableCollection<InjurySubRollEntry> FigureInjuryRolls { get; } = new();

    /// <summary>Plus de saisie manuelle : uniquement modifié par ApplyInjuryRoll, qui synchronise dans
    /// les deux sens (Mort pour un résultat de mort sans équivoque, Actif sinon - y compris quand le
    /// jet est effacé/changé après coup, voir sa doc), ou remis à l'état d'origine par
    /// OnIsOutOfActionChanged.</summary>
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

    /// <summary>Paliers franchis UNIQUEMENT par l'XP accordée en Exploration (ExplorationBonusExperience),
    /// comptés à partir du point où l'étape Progression normale s'est déjà arrêtée (Warrior.Experience +
    /// ExperienceGained) plutôt que depuis Warrior.Experience directement - évite de recompter/refaire
    /// jeter un palier déjà traité par MilestoneCount ci-dessus. Nécessaire parce que l'Exploration
    /// (chapitre "Revenus") a lieu APRÈS la Progression dans la séquence officielle du livre (voir la doc
    /// de classe d'EndOfGameDialogViewModel) : un palier uniquement atteint grâce à cet XP-là ne peut être
    /// détecté qu'une fois l'Exploration résolue, jamais pendant la Progression elle-même. Réutilise
    /// exactement la même mécanique (ExperienceMilestones, AdvanceRollEntry, HeroAdvanceTable/
    /// HenchmanAdvanceTable) via une deuxième carte Progression insérée après l'étape Exploration - voir
    /// EndOfGameDialogViewModel.Steps/WizardStep.IsExplorationAdvance.</summary>
    public int ExplorationMilestoneCount => ExperienceMilestones.MilestonesCrossedCount(Warrior.IsHero,
        Warrior.Experience + ExperienceGained, Warrior.Experience + ExperienceGained + ExplorationBonusExperience);

    public bool HasExplorationMilestone => GainsExperience && ExplorationMilestoneCount > 0;

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
            if (!string.IsNullOrWhiteSpace(InjuryResultText)) parts.Add(ResolvedInjuryText);
            foreach (var sub in MultipleInjuryRolls)
                if (!string.IsNullOrWhiteSpace(sub.InjuryResultText)) parts.Add(sub.InjuryResultText);
            foreach (var figure in FigureInjuryRolls)
                if (!string.IsNullOrWhiteSpace(figure.InjuryResultText)) parts.Add(figure.InjuryResultText);
            if (HasHatredTarget) parts.Add(string.Format(_loc["WarriorsHatredChipFormat"], HatredTargetDisplayName));
            foreach (var advance in AdvanceRolls.Concat(ExplorationAdvanceRolls))
            {
                if (advance.SelectedSkills.Count > 0) parts.Add(advance.SelectedSkillsText);
                else if (advance.HasSpellSelected) parts.Add(advance.SelectedSpell!.Name);
                else if (advance.ResolvedField is not null) parts.Add($"{advance.ResolvedFieldLabel} +1");
                else if (!string.IsNullOrWhiteSpace(advance.ResultText)) parts.Add(advance.ResultText);
            }

            return parts.Count > 0
                ? $"{Name} : {string.Join(", ", parts)}"
                : string.Format(_loc["EndOfGameNoChange"], Name);
        }
    }

    /// <summary>Nombre de Héros de la bande à l'ouverture du wizard (voir EndOfGameDialogViewModel) -
    /// transmis tel quel à chaque AdvanceRollEntry créé par ce row pour AdvanceRollEntry.CanPromote,
    /// voir sa doc pour la limite acceptée (ne suit pas les promotions résolues plus tôt dans la même
    /// Fin de Partie).</summary>
    private readonly int _startingHeroCount;

    /// <summary>Catalogue Injury complet (SpecialRules déjà résolues, voir LibraryService.
    /// GetInjuriesAsync), chargé une seule fois à l'ouverture du wizard - permet à
    /// InjuryBranchSpecialRules de prévisualiser la même résolution que GetOrCreateInjuryAsync fera à
    /// l'enregistrement (WarbandDetailViewModel.EndOfGame), sans attendre la sauvegarde pour afficher
    /// la chip de règle (demande explicite de l'utilisateur 2026-08-25 : "il faut mettre le chip plutôt
    /// que du texte").</summary>
    private readonly IReadOnlyList<Injury> _injuryCatalog;

    public WarriorOutcomeRow(Warrior warrior, string archetypeName, bool gainsExperience, IEnumerable<MagicSchool>? magicSchools = null,
        int startingHeroCount = 0, IReadOnlyList<Injury>? injuryCatalog = null, HiredSword? pitFighterProfile = null,
        IReadOnlyList<EquipmentItem>? pitFighterEquipment = null)
    {
        Warrior = warrior;
        ArchetypeName = archetypeName;
        GainsExperience = gainsExperience;
        _startingHeroCount = startingHeroCount;
        _injuryCatalog = injuryCatalog ?? new List<Injury>();
        MagicSchools = magicSchools?.ToList() ?? new List<MagicSchool>();
        PitFighterProfile = pitFighterProfile;
        PitFighterEquipment = pitFighterEquipment ?? new List<EquipmentItem>();

        foreach (var status in new[] { WarriorStatus.Active, WarriorStatus.Dead })
            _statusByLabel[_loc[$"WarriorStatus{status}"]] = status;

        selectedStatusLabel = _statusByLabel.First(kv => kv.Value == warrior.Status).Key;
    }

    /// <summary>Appelé après un jet de Blessure Grave - synchronise le Statut sur le résultat sans
    /// équivoque de Mort (Héros : 11-15 sur la table D66 ; Homme de main : 1-2 sur la table D6,
    /// mécaniques totalement différentes), DANS LES DEUX SENS : repasse à Actif si le jet est ensuite
    /// changé/effacé vers un résultat qui n'est plus la Mort - bug corrigé le 2026-08-25 (le marquage
    /// Mort restait affiché après avoir retiré/changé le jet, cette méthode ne faisait auparavant que
    /// poser Mort, jamais l'inverse). Le reste (rétablissements, blessures permanentes, "Blessures
    /// multiples"...) ne touche pas le Statut - ces résultats ne sont jamais IsDeath.</summary>
    public void ApplyInjuryRoll(int roll)
    {
        var isDeath = Warrior.IsHero ? SeriousInjuryTable.IsDeath(roll) : HenchmanInjuryTable.IsDeath(roll);
        SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == (isDeath ? WarriorStatus.Dead : WarriorStatus.Active)).Key;
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
            // IsHero || IsHiredSword : un Franc-Tireur reste IsHero=false (bonne table de Blessure D6,
            // bon espacement de paliers Homme de main) mais progresse sur la table Héros ("roll on the
            // Heroes Advancement table as opposed to Henchmen") - le seul endroit où ces deux notions se
            // découplent, voir Warrior.IsHiredSword.
            var entry = new AdvanceRollEntry(AdvanceRolls.Count + 1, Warrior.IsHero || Warrior.IsHiredSword, Warrior, AdvanceRolls, IsSpellcaster, _startingHeroCount);
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            AdvanceRolls.Add(entry);
        }
        while (AdvanceRolls.Count > MilestoneCount)
            AdvanceRolls.RemoveAt(AdvanceRolls.Count - 1);
    }

    /// <summary>Même principe que SyncAdvanceRolls, pour ExplorationAdvanceRolls/ExplorationMilestoneCount
    /// - appelée à chaque changement de DistributedExplorationExperience/LeaderExplorationExperience (le
    /// total d'XP Exploration) ou d'ExperienceGained (son point de départ, voir ExplorationMilestoneCount).</summary>
    private void SyncExplorationAdvanceRolls()
    {
        while (ExplorationAdvanceRolls.Count < ExplorationMilestoneCount)
        {
            var entry = new AdvanceRollEntry(ExplorationAdvanceRolls.Count + 1, Warrior.IsHero || Warrior.IsHiredSword, Warrior, ExplorationAdvanceRolls, IsSpellcaster, _startingHeroCount);
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            ExplorationAdvanceRolls.Add(entry);
        }
        while (ExplorationAdvanceRolls.Count > ExplorationMilestoneCount)
            ExplorationAdvanceRolls.RemoveAt(ExplorationAdvanceRolls.Count - 1);
    }
}
