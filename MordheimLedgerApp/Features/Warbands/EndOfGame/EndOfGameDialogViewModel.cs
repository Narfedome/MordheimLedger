using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>
/// Wizard qui suit la "Séquence d'après-bataille" du livre de règles : Résultat, Hors de combat,
/// une étape Blessure par guerrier coché hors de combat, Expérience, Progression, Trésor,
/// Récapitulatif - dans cet ordre (Blessures Graves puis Expérience puis Revenus, à faire "devant
/// témoin" juste après la partie ; le reste de la séquence - vente de pierre magique, disponibilité
/// des vétérans, personnages spéciaux, achats... - n'est pas dans ce dialog, soit hors périmètre V1
/// soit déjà couvert ailleurs dans l'appli, ex. Recruter/Ajouter un objet sur la carte guerrier).
/// Résultat n'est pas une étape du livre à proprement parler, gardé en premier comme contexte léger
/// pour la phrase d'Historique.
///
/// Steps est reconstruite à chaque accès plutôt que mise en cache (mêmes bases que WarriorRows,
/// jamais réaffectée) : le nombre d'étapes Blessure dépend de IsOutOfAction (une étape "carte
/// pleine écran" par guerrier coché, décision explicite du 2026-08-17 pour ne pas surcharger un seul
/// écran avec tous les guerriers hors de combat à la fois - la coche elle-même reste groupée sur une
/// première étape de vue d'ensemble), et l'étape Progression dépend de HasAnyMilestone. Décocher un
/// guerrier sur l'étape "Hors de combat" fait disparaître son étape Blessure ET efface tout ce qui y
/// avait été saisi (WarriorOutcomeRow.OnIsOutOfActionChanged) - il n'y a plus de blessure à montrer.
/// Le Statut n'est plus une saisie manuelle du tout - voir WarriorOutcomeRow.ApplyInjuryRoll.
/// </summary>
public partial class EndOfGameDialogViewModel : DialogViewModel<bool>
{
    private readonly ISkillPickerService _skillPicker;
    private readonly IDetailDialogService _detailDialogs;
    private readonly int _warbandArchetypeId;

    protected override bool CancelResult => false;

    public ObservableCollection<string> ResultOptions { get; } = new();
    public ObservableCollection<WarriorOutcomeRow> WarriorRows { get; }

    [ObservableProperty]
    private string selectedResult;

    [ObservableProperty]
    private int treasuryFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResultStep))]
    [NotifyPropertyChangedFor(nameof(IsOutOfActionStep))]
    [NotifyPropertyChangedFor(nameof(IsInjuryStep))]
    [NotifyPropertyChangedFor(nameof(IsExperienceStep))]
    [NotifyPropertyChangedFor(nameof(IsAdvanceStep))]
    [NotifyPropertyChangedFor(nameof(IsTreasuryStep))]
    [NotifyPropertyChangedFor(nameof(IsRecapStep))]
    [NotifyPropertyChangedFor(nameof(CurrentInjuryWarrior))]
    [NotifyPropertyChangedFor(nameof(InjuryProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int stepIndex;

    private enum StepKind { Result, OutOfAction, Injury, Experience, Advance, Treasury, Recap }

    private sealed record WizardStep(StepKind Kind, WarriorOutcomeRow? Warrior = null);

    /// <summary>True si au moins un guerrier franchit un palier d'XP cette partie - pilote l'étape
    /// Progression (voir Steps), évalué à chaque accès (pas mis en cache) puisque ça dépend des PX
    /// saisis à l'étape Expérience juste avant.</summary>
    public bool HasAnyMilestone => WarriorRows.Any(r => r.HasMilestone);

    private List<WizardStep> Steps
    {
        get
        {
            var steps = new List<WizardStep> { new(StepKind.Result), new(StepKind.OutOfAction) };
            steps.AddRange(WarriorRows.Where(r => r.IsOutOfAction).Select(r => new WizardStep(StepKind.Injury, r)));
            steps.Add(new(StepKind.Experience));
            if (HasAnyMilestone) steps.Add(new(StepKind.Advance));
            steps.Add(new(StepKind.Treasury));
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
    public bool IsTreasuryStep => Current.Kind == StepKind.Treasury;
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

    public bool CanGoBack => StepIndex > 0;
    public bool IsLastStep => StepIndex >= Steps.Count - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], StepIndex + 1, Steps.Count);

    public EndOfGameDialogViewModel(IEnumerable<Warrior> activeWarriors, ISkillPickerService skillPicker, IDetailDialogService detailDialogs, int warbandArchetypeId)
    {
        _skillPicker = skillPicker;
        _detailDialogs = detailDialogs;
        _warbandArchetypeId = warbandArchetypeId;

        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        selectedResult = ResultOptions[0];

        WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriors.Select(w => new WarriorOutcomeRow(w)));

        // Le nombre d'étapes dépend de IsOutOfAction (une étape Blessure par guerrier coché) et de
        // HasAnyMilestone (étape Progression) - Steps recalcule ça à chaque accès, mais on rafraîchit
        // quand même StepLabel/IsLastStep tout de suite pour que le joueur voie le compte à jour
        // pendant qu'il coche des guerriers, plutôt que d'attendre le prochain Next/Back.
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
        if (StepIndex < Steps.Count - 1) StepIndex++;
    }

    [RelayCommand]
    private void Back()
    {
        if (StepIndex > 0) StepIndex--;
    }

    // Lance les dés à la place du joueur (D66 pour un Héros, D6 pour un Homme de main - deux tables
    // totalement différentes, voir SeriousInjuryTable/HenchmanInjuryTable) et affiche tout de suite le
    // résultat complet dans une popup - le champ ManualRoll reste modifiable ensuite si le joueur
    // préfère un jet physique.
    [RelayCommand]
    private async Task AutoRoll(WarriorOutcomeRow row)
    {
        var roll = row.Warrior.IsHero ? SeriousInjuryTable.RollDice() : HenchmanInjuryTable.RollDice();
        var text = ResolveInjuryText(row.Warrior.IsHero, roll)!;
        row.ManualRoll = roll.ToString();
        row.InjuryResultText = text;
        row.ApplyInjuryRoll(roll);
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    // Pour un jet fait à la table (dés physiques) : le joueur saisit son score dans ManualRoll, ce
    // bouton affiche juste le résultat correspondant sans en tirer un nouveau.
    [RelayCommand]
    private async Task ShowInjuryResult(WarriorOutcomeRow row)
    {
        if (!int.TryParse(row.ManualRoll, out var roll))
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        var text = ResolveInjuryText(row.Warrior.IsHero, roll);
        if (text is null)
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        row.InjuryResultText = text;
        row.ApplyInjuryRoll(roll);
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    // Le jet/la classification (mort ou non) vivent dans Core.Rules, sans dépendance à la
    // localisation - cette résolution de clé -> texte affiché reste ici côté tête MAUI.
    private string? ResolveInjuryText(bool isHero, int roll)
    {
        bool found;
        string key;
        if (isHero) found = SeriousInjuryTable.TryGetTextKey(roll, out key);
        else found = HenchmanInjuryTable.TryGetTextKey(roll, out key);
        return found ? Loc[key] : null;
    }

    // Résultat "Blessures multiples" (16/21, Héros uniquement) : le joueur lance 1D6 pour savoir
    // combien de sous-jets faire sur cette même table (règle du livre : "Roll D6 times on this
    // table" = un nombre déterminé par 1D6, pas un compte fixe) - AutoRollMultipleInjuryCount tire ce
    // 1D6 à la place du joueur, ShowMultipleInjuryCountResult valide un jet physique saisi à la main.
    // Les deux délèguent à WarriorOutcomeRow.SetMultipleInjuryCount qui (re)peuple MultipleInjuryRolls.
    [RelayCommand]
    private void AutoRollMultipleInjuryCount(WarriorOutcomeRow row) => row.SetMultipleInjuryCount(Random.Shared.Next(1, 7));

    [RelayCommand]
    private async Task ShowMultipleInjuryCountResult(WarriorOutcomeRow row)
    {
        if (!int.TryParse(row.MultipleInjuryCountRoll, out var count) || count is < 1 or > 6)
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidMultipleInjuryCount"]);
            return;
        }

        row.SetMultipleInjuryCount(count);
    }

    // Un résultat "Blessures multiples" déclenche autant de sous-jets sur cette même table que le 1D6
    // ci-dessus l'a déterminé - WarriorOutcomeRow.SetMultipleInjuryCount a déjà peuplé les entrées
    // vides. Même paire de commandes Tirer/Voir le résultat que le jet principal, y compris pour un
    // résultat qui devrait en théorie être relancé (Mort/Capturé/Blessures multiples, cf. livre) -
    // décision explicite : l'appli n'impose ni ne relance rien elle-même, le résultat du joueur est
    // accepté tel quel comme n'importe quel autre jet de cette table.
    [RelayCommand]
    private async Task AutoRollSubInjury(InjurySubRollEntry entry)
    {
        var roll = SeriousInjuryTable.RollDice();
        var text = ResolveInjuryText(true, roll)!;
        entry.ManualRoll = roll.ToString();
        entry.InjuryResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    [RelayCommand]
    private async Task ShowSubInjuryResult(InjurySubRollEntry entry)
    {
        if (!int.TryParse(entry.ManualRoll, out var roll))
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        var text = ResolveInjuryText(true, roll);
        if (text is null)
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        entry.InjuryResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    // Un guerrier peut franchir plusieurs paliers d'un coup - chaque AdvanceRollEntry (une par palier,
    // voir WarriorOutcomeRow.SyncAdvanceRolls) est un jet 2D6 indépendant sur la table de progression,
    // même pattern que AutoRoll/ShowInjuryResult mais purement descriptif : aucune stat n'est modifiée
    // automatiquement (les sous-jets 1D6 des résultats 6/8/9 et le choix CC/CT du 7 restent à résoudre
    // par le joueur, cf. HeroAdvanceTable/HenchmanAdvanceTable).
    [RelayCommand]
    private async Task AutoRollAdvance(AdvanceRollEntry entry)
    {
        var roll = entry.IsHero ? HeroAdvanceTable.RollDice() : HenchmanAdvanceTable.RollDice();
        var text = ResolveAdvanceText(entry.IsHero, roll)!;
        entry.ManualRoll = roll.ToString();
        entry.ResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    [RelayCommand]
    private async Task ShowAdvanceResult(AdvanceRollEntry entry)
    {
        if (!int.TryParse(entry.ManualRoll, out var roll))
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidAdvanceRoll"]);
            return;
        }

        var text = ResolveAdvanceText(entry.IsHero, roll);
        if (text is null)
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidAdvanceRoll"]);
            return;
        }

        entry.ResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    private string? ResolveAdvanceText(bool isHero, int roll)
    {
        bool found;
        string key;
        if (isHero) found = HeroAdvanceTable.TryGetTextKey(roll, out key);
        else found = HenchmanAdvanceTable.TryGetTextKey(roll, out key);
        return found ? Loc[key] : null;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(MilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasMilestone))]
    private int experienceGained;

    partial void OnExperienceGainedChanged(int value) => SyncAdvanceRolls();

    [ObservableProperty]
    private bool isOutOfAction;

    /// <summary>Décoché sur l'étape "Hors de combat" : ce guerrier n'a plus d'étape Blessure dans le
    /// wizard (voir EndOfGameDialogViewModel.Steps), donc plus rien à y montrer - tout ce qui avait pu
    /// être saisi pour lui (jet principal, sous-jets de Blessures multiples, statut dérivé) est
    /// effacé plutôt que laissé orphelin.</summary>
    partial void OnIsOutOfActionChanged(bool value)
    {
        if (value) return;

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

    /// <summary>Le score D66 (Héros) ou D6 (Homme de main) - saisi à la main (jet physique) ou rempli
    /// par AutoRoll.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMultipleInjuriesSection))]
    private string manualRoll = string.Empty;

    /// <summary>Si le jet principal est refait vers un résultat qui n'est plus "Blessures multiples",
    /// les sous-jets précédemment saisis n'ont plus de sens - on les efface plutôt que de les laisser
    /// affichés pour un résultat qui ne les déclenche plus.</summary>
    partial void OnManualRollChanged(string value)
    {
        if (!Warrior.IsHero || ShowMultipleInjuriesSection) return;
        if (MultipleInjuryCountRoll.Length == 0 && MultipleInjuryRolls.Count == 0) return;

        MultipleInjuryCountRoll = string.Empty;
        MultipleInjuryRolls.Clear();
        OnPropertyChanged(nameof(HasMultipleInjuryRolls));
        OnPropertyChanged(nameof(SummaryText));
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
    /// rempli par AutoRollMultipleInjuryCount, voir SetMultipleInjuryCount.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string multipleInjuryCountRoll = string.Empty;

    /// <summary>Un sous-jet D66 par point du 1D6 ci-dessus - peuplée par SetMultipleInjuryCount une
    /// fois ce 1D6 résolu. Vide tant qu'il ne l'est pas, et pour tout guerrier/résultat qui n'est pas
    /// concerné par les Blessures multiples.</summary>
    public ObservableCollection<InjurySubRollEntry> MultipleInjuryRolls { get; } = new();
    public bool HasMultipleInjuryRolls => MultipleInjuryRolls.Count > 0;

    /// <summary>Plus de saisie manuelle : uniquement modifié par ApplyInjuryRoll (résultat "Mort",
    /// jets 11-15) ou remis à l'état d'origine par OnIsOutOfActionChanged. Reste sur Warrior.Status
    /// (donc "Actif") pour tout le reste, y compris les rétablissements et le résultat "Blessures
    /// multiples" (16/21, ambigu tant que les sous-jets ne sont pas résolus).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(IsDead))]
    private string selectedStatusLabel = string.Empty;

    public WarriorStatus Status => _statusByLabel.GetValueOrDefault(SelectedStatusLabel, Warrior.Status);
    public bool IsDead => Status == WarriorStatus.Dead;

    /// <summary>Nombre de cases à bord épais franchies par les PX gagnés cette partie (voir
    /// ExperienceMilestones) - peut dépasser 1 si le guerrier cumule assez de PX pour sauter
    /// plusieurs paliers d'un coup, d'où AdvanceRolls plutôt qu'un jet unique.</summary>
    public int MilestoneCount => ExperienceMilestones.MilestonesCrossedCount(Warrior.IsHero, Warrior.Experience, Warrior.Experience + ExperienceGained);
    public bool HasMilestone => MilestoneCount > 0;

    /// <summary>Héros et Hommes de main utilisent deux tables de Blessures Graves totalement
    /// différentes (D66 vs D6, voir SeriousInjuryTable/HenchmanInjuryTable) - ce label et ce
    /// placeholder gardent la distinction visible sur l'étape Blessure de chaque guerrier.</summary>
    public string RoleLabel => _loc[Warrior.IsHero ? "WarriorRoleHeroSingular" : "WarriorRoleHenchmanSingular"];
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

    public WarriorOutcomeRow(Warrior warrior)
    {
        Warrior = warrior;

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
    /// s'ajouter aux sous-jets déjà saisis.</summary>
    public void SetMultipleInjuryCount(int count)
    {
        MultipleInjuryCountRoll = count.ToString();
        MultipleInjuryRolls.Clear();
        for (var i = 1; i <= count; i++)
        {
            var entry = new InjurySubRollEntry(i, count);
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            MultipleInjuryRolls.Add(entry);
        }

        OnPropertyChanged(nameof(HasMultipleInjuryRolls));
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

    /// <summary>Le score 2D6 - saisi à la main (jet physique) ou rempli par AutoRollAdvance.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkillResult))]
    private string manualRoll = string.Empty;

    /// <summary>Texte descriptif du résultat une fois consulté - purement informatif, voir
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

/// <summary>One D66 sub-roll stacked from a "Blessures multiples" result (16/21) on the Heroes'
/// Serious Injuries table - see WarriorOutcomeRow.MultipleInjuryRolls/SetMultipleInjuryCount. Same
/// Tirer/Voir le résultat pattern as the main injury roll - including accepting a sub-roll that lands
/// on Dead/Captured/Multiple Injuries again, which the rulebook says to re-roll but the app leaves to
/// the player rather than enforcing itself (see SeriousInjuryTable's doc comment).</summary>
public partial class InjurySubRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public int Index { get; }
    public int Total { get; }
    public string Label => string.Format(_loc["EndOfGameMultipleInjuryLabel"], Index, Total);

    [ObservableProperty]
    private string manualRoll = string.Empty;

    [ObservableProperty]
    private string injuryResultText = string.Empty;

    public InjurySubRollEntry(int index, int total)
    {
        Index = index;
        Total = total;
    }
}
